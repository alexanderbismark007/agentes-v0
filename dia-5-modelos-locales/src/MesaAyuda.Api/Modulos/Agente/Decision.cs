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

            var accion = LeerTexto(raiz, "accion")?.Trim();
            var respuesta = LeerTexto(raiz, "respuesta");

            // Si el modelo redactó una respuesta, esa es su intención, sin
            // importar cómo haya rotulado la acción.
            if (!string.IsNullOrWhiteSpace(respuesta))
            {
                return Final(respuesta);
            }

            // El nombre de la herramienta se busca donde el modelo lo haya
            // puesto. Los modelos pequeños suelen rotular mal el campo "accion"
            // y escribir ahí el nombre de la herramienta, o poner el nombre
            // correcto en "herramienta" pero con una acción mal escrita.
            // Exigir el rótulo exacto haría que esas salidas se interpretaran
            // como respuesta final y el JSON crudo terminaría mostrándose al
            // usuario.
            var herramienta = LeerTexto(raiz, "herramienta")
                              ?? LeerTexto(raiz, "tool")
                              ?? LeerTexto(raiz, "nombre");

            if (string.IsNullOrWhiteSpace(herramienta)
                && !string.IsNullOrWhiteSpace(accion)
                && !EsRotuloDeAccion(accion))
            {
                herramienta = accion;
            }

            if (!string.IsNullOrWhiteSpace(herramienta))
            {
                return new Decision
                {
                    EsRespuestaFinal = false,
                    Herramienta = herramienta,
                    Argumentos = LeerArgumentos(raiz)
                };
            }

            if (string.Equals(accion, "herramienta", StringComparison.OrdinalIgnoreCase))
            {
                return Final("El agente indicó usar una herramienta pero no especificó cuál.");
            }

            // Un objeto JSON que no se pudo interpretar no debe mostrarse tal
            // cual: el usuario recibiría la mecánica interna del agente.
            return Final("El agente no produjo una respuesta interpretable para esa consulta.");
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

    /// <summary>
    /// Indica si el valor del campo "accion" es uno de los rótulos previstos y
    /// no el nombre de una herramienta escrito en el lugar equivocado.
    /// </summary>
    private static bool EsRotuloDeAccion(string accion) =>
        accion.Equals("herramienta", StringComparison.OrdinalIgnoreCase)
        || accion.Equals("responder", StringComparison.OrdinalIgnoreCase)
        || accion.Equals("tool", StringComparison.OrdinalIgnoreCase)
        || accion.Equals("respuesta", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Lee los argumentos aceptando los nombres que usan distintos modelos.
    /// </summary>
    private static JsonElement LeerArgumentos(JsonElement raiz)
    {
        foreach (var nombre in (string[])["argumentos", "arguments", "parametros", "input"])
        {
            if (raiz.TryGetProperty(nombre, out var propiedad)
                && propiedad.ValueKind == JsonValueKind.Object)
            {
                return propiedad.Clone();
            }
        }

        return Vacio;
    }

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
