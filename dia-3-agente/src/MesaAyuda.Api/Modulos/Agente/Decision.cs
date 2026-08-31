using System.Text.Json;

namespace MesaAyuda.Api.Modulos.Agente;

/// <summary>
/// Lo que el modelo decidió hacer en una iteración: invocar una herramienta o
/// dar la respuesta final.
///
/// Interpretar esta decisión es el punto más frágil de todo agente, porque
/// depende de que el modelo respete un formato. Por eso aquí se es tolerante
/// con la forma y estricto con el contenido: se acepta un JSON envuelto en
/// texto o en un bloque de código, pero si no se puede determinar la intención
/// se trata como respuesta final en lugar de reintentar a ciegas.
/// </summary>
public sealed record Decision
{
    private static readonly JsonElement Vacio = JsonDocument.Parse("{}").RootElement.Clone();

    public bool EsRespuestaFinal { get; private init; }

    public string? Respuesta { get; private init; }

    public string? Herramienta { get; private init; }

    public JsonElement Argumentos { get; private init; } = Vacio;

    public static Decision Interpretar(string contenido)
    {
        var json = ExtraerJson(contenido);

        if (json is null)
        {
            // Sin JSON reconocible, se toma el texto como la respuesta.
            return Final(contenido.Trim());
        }

        try
        {
            using var documento = JsonDocument.Parse(json);
            var raiz = documento.RootElement;

            var accion = LeerTexto(raiz, "accion")?.ToLowerInvariant();

            if (accion == "herramienta")
            {
                var herramienta = LeerTexto(raiz, "herramienta");

                if (string.IsNullOrWhiteSpace(herramienta))
                {
                    return Final("El agente indicó usar una herramienta pero no especificó cuál.");
                }

                var argumentos = raiz.TryGetProperty("argumentos", out var propiedad)
                                 && propiedad.ValueKind == JsonValueKind.Object
                    ? propiedad.Clone()
                    : Vacio;

                return new Decision
                {
                    EsRespuestaFinal = false,
                    Herramienta = herramienta,
                    Argumentos = argumentos
                };
            }

            var respuesta = LeerTexto(raiz, "respuesta");

            return Final(string.IsNullOrWhiteSpace(respuesta)
                ? contenido.Trim()
                : respuesta);
        }
        catch (JsonException)
        {
            return Final(contenido.Trim());
        }
    }

    private static Decision Final(string respuesta) => new()
    {
        EsRespuestaFinal = true,
        Respuesta = respuesta
    };

    private static string? LeerTexto(JsonElement raiz, string propiedad) =>
        raiz.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;

    /// <summary>
    /// Aísla el objeto JSON más externo del texto, tolerando que el modelo lo
    /// envuelva en explicaciones o en un bloque de código.
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
