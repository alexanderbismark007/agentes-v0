using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MesaAyuda.Api.Nucleo.Proveedores;

/// <summary>
/// Proveedor determinista que no realiza llamadas de red.
///
/// Cubre los dos usos que el sistema hace de un modelo de lenguaje:
///
/// - Clasificar una solicitud, mediante reglas de palabras clave.
/// - Responder una consulta documental, seleccionando del contexto recibido la
///   oración que mejor responde la pregunta y citando su fragmento de origen.
///
/// No redacta ni razona: extrae. Por eso no reemplaza a un modelo real, y ese
/// contraste es justamente lo que conviene mostrar en clase. Su valor es que
/// permite ejecutar y probar la arquitectura completa sin credenciales, sin
/// costo y con resultados reproducibles, de modo que al conectar un modelo real
/// se pueda medir exactamente qué aporta de más.
/// </summary>
public sealed partial class ProveedorSimulado : IProveedorLenguaje
{
    private static readonly Dictionary<string, string[]> SenalesPorCategoria = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Academica"] = ["materia", "docente", "nota", "examen", "inscripcion", "inscripción", "semestre", "kardex", "titulacion", "titulación", "malla"],
        ["Administrativa"] = ["certificado", "tramite", "trámite", "solicitud", "carta", "constancia", "archivo", "secretaria", "secretaría"],
        ["Tecnologica"] = ["sistema", "plataforma", "correo", "contrasena", "contraseña", "usuario", "acceso", "servidor", "aplicacion", "aplicación", "red", "internet", "wifi"],
        ["Financiera"] = ["pago", "deuda", "arancel", "factura", "beca", "descuento", "cuota", "reembolso", "matricula", "matrícula"],
        ["Infraestructura"] = ["aula", "laboratorio", "silla", "iluminacion", "iluminación", "bano", "baño", "agua", "electricidad", "limpieza", "ascensor"]
    };

    private static readonly HashSet<string> PalabrasVacias = new(StringComparer.Ordinal)
    {
        "el", "la", "los", "las", "un", "una", "unos", "unas", "de", "del", "al", "a", "con",
        "en", "para", "por", "sin", "sobre", "y", "o", "u", "e", "que", "se", "su", "sus", "lo",
        "es", "son", "ser", "este", "esta", "estos", "estas", "como", "mas", "pero", "si", "no",
        "cual", "cuales", "cuanto", "cuantos", "cuanta", "cuantas", "donde", "cuando", "quien"
    };

    [GeneratedRegex(@"^\[(?<numero>\d+)\]\s*(?<ubicacion>.*)$", RegexOptions.Multiline)]
    private static partial Regex EncabezadoFragmento();

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex FinDeOracion();

    public string Nombre => "simulado";

    public Task<RespuestaLenguaje> CompletarAsync(PeticionLenguaje peticion, CancellationToken cancelacion = default)
    {
        var texto = string.Join('\n', peticion.Mensajes
            .Where(m => m.Rol == RolMensaje.Usuario)
            .Select(m => m.Contenido));

        var contenido = peticion.RespuestaJson
            ? ResponderClasificacion(texto)
            : ResponderConsulta(texto);

        var respuesta = new RespuestaLenguaje(
            contenido,
            Modelo: "reglas-locales-v1",
            TokensEntrada: EstimarTokens(texto),
            TokensSalida: EstimarTokens(contenido));

        return Task.FromResult(respuesta);
    }

    // ----------------------------------------------------------------------
    // Clasificación de solicitudes
    // ----------------------------------------------------------------------

    private static string ResponderClasificacion(string texto)
    {
        var normalizado = texto.ToLowerInvariant();

        var puntajes = SenalesPorCategoria
            .Select(par => new
            {
                Categoria = par.Key,
                Coincidencias = par.Value.Where(senal => normalizado.Contains(senal, StringComparison.Ordinal)).ToArray()
            })
            .Where(x => x.Coincidencias.Length > 0)
            .OrderByDescending(x => x.Coincidencias.Length)
            .ThenBy(x => x.Categoria, StringComparer.Ordinal)
            .ToList();

        if (puntajes.Count == 0)
        {
            return Serializar("Otra", 0.25d, "No se encontraron términos característicos de ninguna categoría.");
        }

        var mejor = puntajes[0];
        var total = puntajes.Sum(x => x.Coincidencias.Length);

        // La confianza combina cuánto domina la categoría ganadora sobre el
        // resto y cuántas señales encontró en términos absolutos.
        var dominancia = (double)mejor.Coincidencias.Length / total;
        var soporte = Math.Min(mejor.Coincidencias.Length / 3d, 1d);
        var confianza = Math.Clamp(Math.Round(0.45d + (0.35d * dominancia) + (0.20d * soporte), 2), 0d, 1d);

        return Serializar(
            mejor.Categoria,
            confianza,
            $"Se encontraron los términos: {string.Join(", ", mejor.Coincidencias)}.");
    }

    private static string Serializar(string categoria, double confianza, string justificacion) =>
        JsonSerializer.Serialize(new { categoria, confianza, justificacion }, OpcionesJson.Predeterminadas);

    // ----------------------------------------------------------------------
    // Consulta documental
    // ----------------------------------------------------------------------

    /// <summary>
    /// Selecciona del contexto la oración que más términos comparte con la
    /// pregunta y la devuelve citando el fragmento del que proviene.
    /// </summary>
    private static string ResponderConsulta(string texto)
    {
        var (contexto, pregunta) = SepararPregunta(texto);

        if (string.IsNullOrWhiteSpace(contexto) || string.IsNullOrWhiteSpace(pregunta))
        {
            return "No se recibió contexto suficiente para responder la consulta.";
        }

        var terminosPregunta = Segmentar(pregunta);
        if (terminosPregunta.Count == 0)
        {
            return "La pregunta no contiene términos suficientes para buscar una respuesta.";
        }

        var mejorOracion = string.Empty;
        var mejorFragmento = 0;
        var mejorPuntaje = 0;

        foreach (var (numero, cuerpo) in SepararFragmentos(contexto))
        {
            foreach (var oracion in FinDeOracion().Split(cuerpo))
            {
                var limpia = oracion.Trim();
                if (limpia.Length < 25)
                {
                    continue;
                }

                var puntaje = Segmentar(limpia).Intersect(terminosPregunta).Count();

                if (puntaje > mejorPuntaje)
                {
                    mejorPuntaje = puntaje;
                    mejorOracion = limpia;
                    mejorFragmento = numero;
                }
            }
        }

        if (mejorPuntaje == 0)
        {
            return "Los fragmentos recuperados no contienen información que responda esa pregunta.";
        }

        return $"{mejorOracion} [{mejorFragmento}]";
    }

    private static (string Contexto, string Pregunta) SepararPregunta(string texto)
    {
        var marca = texto.LastIndexOf("Pregunta:", StringComparison.OrdinalIgnoreCase);

        return marca < 0
            ? (texto, string.Empty)
            : (texto[..marca], texto[(marca + "Pregunta:".Length)..].Trim());
    }

    /// <summary>
    /// Recorre los bloques numerados que arma el servicio de consultas y
    /// devuelve el número de cada fragmento junto con su texto.
    /// </summary>
    private static IEnumerable<(int Numero, string Cuerpo)> SepararFragmentos(string contexto)
    {
        var encabezados = EncabezadoFragmento().Matches(contexto);

        for (var i = 0; i < encabezados.Count; i++)
        {
            var actual = encabezados[i];
            var inicio = actual.Index + actual.Length;
            var fin = i + 1 < encabezados.Count ? encabezados[i + 1].Index : contexto.Length;

            if (int.TryParse(actual.Groups["numero"].Value, out var numero))
            {
                yield return (numero, contexto[inicio..fin].Trim());
            }
        }
    }

    // ----------------------------------------------------------------------
    // Utilidades
    // ----------------------------------------------------------------------

    private static HashSet<string> Segmentar(string texto)
    {
        var terminos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var parte in texto.Split(
            [' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '"', '\'', '/', '-'],
            StringSplitOptions.RemoveEmptyEntries))
        {
            var termino = Normalizar(parte);

            if (termino.Length >= 4 && !PalabrasVacias.Contains(termino))
            {
                terminos.Add(termino);
            }
        }

        return terminos;
    }

    /// <summary>
    /// Pasa a minúsculas y quita los acentos, para que "días" y "dias" cuenten
    /// como el mismo término.
    /// </summary>
    private static string Normalizar(string palabra)
    {
        var descompuesta = palabra.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var constructor = new StringBuilder(descompuesta.Length);

        foreach (var caracter in descompuesta)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(caracter))
            {
                constructor.Append(caracter);
            }
        }

        return constructor.ToString();
    }

    /// <summary>
    /// Aproximación suficiente para registrar consumo sin depender de un
    /// tokenizador externo: alrededor de cuatro caracteres por token.
    /// </summary>
    private static int EstimarTokens(string texto) =>
        string.IsNullOrEmpty(texto) ? 0 : (int)Math.Ceiling(texto.Length / 4d);
}
