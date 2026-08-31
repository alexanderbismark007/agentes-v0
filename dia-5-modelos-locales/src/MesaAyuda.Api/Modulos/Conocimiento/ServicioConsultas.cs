using System.Text;
using MesaAyuda.Api.Dominio.Conocimiento;
using MesaAyuda.Api.Modulos.Conocimiento.Contratos;
using MesaAyuda.Api.Nucleo.Configuracion;
using MesaAyuda.Api.Nucleo.Proveedores;
using Microsoft.Extensions.Options;

namespace MesaAyuda.Api.Modulos.Conocimiento;

/// <summary>
/// Responde preguntas sobre los documentos institucionales indexados.
///
/// El procedimiento tiene tres pasos y el orden importa: primero se recupera,
/// después se arma el contexto y solo al final se genera la respuesta. El
/// modelo nunca contesta desde su conocimiento previo, sino exclusivamente a
/// partir de los fragmentos recuperados. Si no se recupera nada pertinente,
/// no se llama al modelo: se responde que no hay información.
/// </summary>
public sealed class ServicioConsultas(
    IBuscadorSemantico buscador,
    IProveedorEmbeddings proveedorEmbeddings,
    IProveedorLenguaje proveedorLenguaje,
    IOptions<OpcionesConocimiento> opciones,
    ILogger<ServicioConsultas> registro)
{
    private readonly OpcionesConocimiento _configuracion = opciones.Value;

    private const string Instruccion =
        "Eres el asistente documental de una universidad pública. Respondes preguntas " +
        "usando exclusivamente los fragmentos de reglamento que se te entregan.\n\n" +
        "Reglas que debes cumplir siempre:\n" +
        "1. Responde únicamente con lo que dicen los fragmentos. No agregues conocimiento propio.\n" +
        "2. Cita la fuente de cada afirmación indicando el número de fragmento entre corchetes, por ejemplo [1].\n" +
        "3. Si los fragmentos no contienen la respuesta, dilo con claridad en lugar de suponer.\n" +
        "4. No inventes artículos, plazos, montos ni requisitos que no figuren en el texto.\n" +
        "5. Responde en español, de forma breve y concreta.";

    public async Task<RespuestaConsulta> ResponderAsync(
        string pregunta,
        NivelAcceso nivelAcceso,
        CancellationToken cancelacion = default)
    {
        var inicio = DateTimeOffset.UtcNow;

        // Paso 1: la pregunta se convierte al mismo espacio vectorial en el que
        // se indexaron los fragmentos. Debe usarse el mismo modelo, o los
        // vectores no serían comparables.
        var vectorPregunta = await proveedorEmbeddings.GenerarAsync(pregunta, cancelacion);

        var recuperados = await buscador.BuscarAsync(
            vectorPregunta,
            _configuracion.FragmentosRecuperados,
            nivelAcceso,
            cancelacion);

        // Se descarta lo que apenas se parece: un fragmento poco pertinente en
        // el contexto es peor que un fragmento ausente, porque induce al modelo
        // a construir una respuesta sobre material que no viene al caso.
        var pertinentes = recuperados
            .Where(r => r.Similitud >= _configuracion.SimilitudMinima)
            .ToArray();

        if (pertinentes.Length == 0)
        {
            registro.LogInformation(
                "Sin fragmentos pertinentes para la consulta. Mejor similitud: {Similitud}",
                recuperados.Count > 0 ? recuperados.Max(r => r.Similitud) : 0d);

            return new RespuestaConsulta
            {
                Pregunta = pregunta,
                Respuesta =
                    "No encontré información sobre esa consulta en los documentos disponibles. " +
                    "Le sugiero reformular la pregunta o dirigirla a la unidad correspondiente.",
                TieneRespaldo = false,
                Fuentes = [],
                ConfianzaRecuperacion = recuperados.Count > 0 ? Math.Round(recuperados.Max(r => r.Similitud), 3) : 0d,
                ProveedorLenguaje = proveedorLenguaje.Nombre,
                ProveedorEmbeddings = proveedorEmbeddings.Nombre,
                MilisegundosTotales = (int)(DateTimeOffset.UtcNow - inicio).TotalMilliseconds
            };
        }

        // Paso 2: se arma el contexto numerando los fragmentos, para que el
        // modelo pueda referirse a ellos y la cita sea verificable.
        var contexto = ConstruirContexto(pertinentes);

        var peticion = new PeticionLenguaje
        {
            Mensajes =
            [
                MensajeLenguaje.Sistema(Instruccion),
                MensajeLenguaje.Usuario($"Fragmentos disponibles:\n\n{contexto}\n\nPregunta: {pregunta}")
            ],
            Temperatura = 0d,
            MaximoTokens = 700
        };

        // Paso 3: generación acotada al contexto entregado.
        var respuesta = await proveedorLenguaje.CompletarAsync(peticion, cancelacion);

        return new RespuestaConsulta
        {
            Pregunta = pregunta,
            Respuesta = respuesta.Contenido,
            TieneRespaldo = true,
            Fuentes = pertinentes
                .Select((f, indice) => new FuenteCitada
                {
                    Numero = indice + 1,
                    DocumentoId = f.DocumentoId,
                    Documento = f.TituloDocumento,
                    Referencia = f.Referencia,
                    Extracto = Recortar(f.Contenido, 300),
                    Similitud = Math.Round(f.Similitud, 3)
                })
                .ToArray(),
            ConfianzaRecuperacion = Math.Round(pertinentes.Max(f => f.Similitud), 3),
            ProveedorLenguaje = proveedorLenguaje.Nombre,
            ProveedorEmbeddings = proveedorEmbeddings.Nombre,
            ModeloLenguaje = respuesta.Modelo,
            TokensEntrada = respuesta.TokensEntrada,
            TokensSalida = respuesta.TokensSalida,
            MilisegundosTotales = (int)(DateTimeOffset.UtcNow - inicio).TotalMilliseconds
        };
    }

    internal static string ConstruirContexto(IReadOnlyList<FragmentoRecuperado> fragmentos)
    {
        var constructor = new StringBuilder();

        for (var i = 0; i < fragmentos.Count; i++)
        {
            var fragmento = fragmentos[i];

            var ubicacion = string.IsNullOrWhiteSpace(fragmento.Referencia)
                ? fragmento.TituloDocumento
                : $"{fragmento.TituloDocumento}, {fragmento.Referencia}";

            constructor
                .Append('[').Append(i + 1).Append("] ")
                .Append(ubicacion)
                .AppendLine()
                .AppendLine(fragmento.Contenido)
                .AppendLine();
        }

        return constructor.ToString().TrimEnd();
    }

    private static string Recortar(string texto, int longitud) =>
        texto.Length <= longitud ? texto : texto[..longitud].TrimEnd() + "...";
}
