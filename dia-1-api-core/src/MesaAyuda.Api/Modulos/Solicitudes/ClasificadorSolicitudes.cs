using System.Text.Json;
using MesaAyuda.Api.Dominio.Solicitudes;
using MesaAyuda.Api.Nucleo.Proveedores;

namespace MesaAyuda.Api.Modulos.Solicitudes;

/// <summary>
/// Resultado de clasificar el texto de una solicitud.
/// </summary>
/// <param name="Categoria">Categoría propuesta.</param>
/// <param name="Confianza">Grado de certeza entre 0 y 1.</param>
/// <param name="Justificacion">Explicación breve de la decisión.</param>
/// <param name="Origen">Componente que produjo la sugerencia.</param>
public sealed record ResultadoClasificacion(
    CategoriaSolicitud Categoria,
    double Confianza,
    string Justificacion,
    string Origen);

/// <summary>
/// Propone la categoría de una solicitud a partir de su texto.
/// </summary>
public interface IClasificadorSolicitudes
{
    Task<ResultadoClasificacion> ClasificarAsync(string titulo, string descripcion, CancellationToken cancelacion = default);
}

/// <summary>
/// Clasificador que delega el análisis en el proveedor de lenguaje configurado.
///
/// La sugerencia nunca reemplaza la decisión del operador: se guarda junto a la
/// solicitud como dato auxiliar y auditable. Si el proveedor falla o devuelve
/// algo que no se puede interpretar, la solicitud se registra igual con la
/// categoría Otra y confianza cero.
/// </summary>
public sealed class ClasificadorSolicitudes(
    IProveedorLenguaje proveedor,
    ILogger<ClasificadorSolicitudes> registro) : IClasificadorSolicitudes
{
    private const string Instruccion =
        "Eres un asistente de la mesa de ayuda de una universidad pública.\n" +
        "Clasifica la solicitud recibida en exactamente una de estas categorías:\n" +
        "Academica, Administrativa, Tecnologica, Financiera, Infraestructura, Otra.\n\n" +
        "Responde únicamente con un objeto JSON con esta forma:\n" +
        "{\"categoria\":\"<categoría>\",\"confianza\":<número entre 0 y 1>,\"justificacion\":\"<una oración>\"}\n\n" +
        "No agregues texto fuera del JSON.";

    public async Task<ResultadoClasificacion> ClasificarAsync(
        string titulo,
        string descripcion,
        CancellationToken cancelacion = default)
    {
        var peticion = new PeticionLenguaje
        {
            Mensajes =
            [
                MensajeLenguaje.Sistema(Instruccion),
                MensajeLenguaje.Usuario($"Título: {titulo}\nDescripción: {descripcion}")
            ],
            Temperatura = 0d,
            MaximoTokens = 200,
            RespuestaJson = true
        };

        try
        {
            var respuesta = await proveedor.CompletarAsync(peticion, cancelacion);
            return Interpretar(respuesta.Contenido, proveedor.Nombre);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            // Una solicitud no puede perderse porque el clasificador falle:
            // se registra el problema y el alta continúa sin sugerencia.
            registro.LogWarning(excepcion, "No se pudo clasificar la solicitud con el proveedor {Proveedor}", proveedor.Nombre);
            return new ResultadoClasificacion(CategoriaSolicitud.Otra, 0d, "El clasificador no estuvo disponible.", proveedor.Nombre);
        }
    }

    internal static ResultadoClasificacion Interpretar(string contenido, string origen)
    {
        var json = ExtraerJson(contenido);
        if (json is null)
        {
            return new ResultadoClasificacion(CategoriaSolicitud.Otra, 0d, "La respuesta del modelo no contenía JSON.", origen);
        }

        try
        {
            using var documento = JsonDocument.Parse(json);
            var raiz = documento.RootElement;

            var textoCategoria = raiz.TryGetProperty("categoria", out var propCategoria)
                ? propCategoria.GetString()
                : null;

            var categoria = Enum.TryParse<CategoriaSolicitud>(textoCategoria, ignoreCase: true, out var interpretada)
                ? interpretada
                : CategoriaSolicitud.Otra;

            var confianza = raiz.TryGetProperty("confianza", out var propConfianza)
                            && propConfianza.ValueKind is JsonValueKind.Number
                ? Math.Clamp(propConfianza.GetDouble(), 0d, 1d)
                : 0d;

            var justificacion = raiz.TryGetProperty("justificacion", out var propJustificacion)
                ? propJustificacion.GetString() ?? string.Empty
                : string.Empty;

            return new ResultadoClasificacion(categoria, confianza, justificacion, origen);
        }
        catch (JsonException)
        {
            return new ResultadoClasificacion(CategoriaSolicitud.Otra, 0d, "La respuesta del modelo no era JSON válido.", origen);
        }
    }

    /// <summary>
    /// Aísla el primer objeto JSON del texto. Algunos modelos envuelven la
    /// respuesta en bloques de código o agregan texto alrededor.
    /// </summary>
    private static string? ExtraerJson(string contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido))
        {
            return null;
        }

        var inicio = contenido.IndexOf('{');
        var fin = contenido.LastIndexOf('}');

        return inicio >= 0 && fin > inicio ? contenido[inicio..(fin + 1)] : null;
    }
}
