using System.Text.Json;
using System.Text.RegularExpressions;

namespace MesaAyuda.Api.Nucleo.Proveedores;

/// <summary>
/// Modo agente del proveedor simulado: elige qué herramienta usar a partir de
/// la pregunta y, una vez recibido el resultado, redacta la respuesta final.
///
/// La selección se hace con reglas de palabras clave. Es una imitación honesta
/// del comportamiento de un modelo real: reproduce el ciclo completo de
/// decisión, ejecución y respuesta, de modo que toda la mecánica del agente
/// (traza, límites, aprobación humana) pueda demostrarse y probarse sin
/// depender de un servicio externo. Lo que no reproduce es el juicio: ante una
/// pregunta ambigua elegirá mal donde un modelo real acertaría, y ese contraste
/// es justamente lo que conviene mostrar al conectar un proveedor de verdad.
/// </summary>
public sealed partial class ProveedorSimulado
{
    private const string MarcaCatalogo = "Herramientas disponibles:";
    private const string MarcaResultado = "Resultado de ";

    [GeneratedRegex(@"SOL-\d{4}-\d{6}", RegexOptions.IgnoreCase)]
    private static partial Regex CodigoSolicitud();

    private static readonly string[] SenalesReglamento =
        ["reglamento", "articulo", "artículo", "plazo", "plazos", "requisito", "requisitos",
         "norma", "normativa", "procedimiento", "dice", "establece", "permitido", "derecho",
         "conservan", "notificacion", "notificación", "reclamo", "vigencia"];

    private static readonly string[] SenalesCambio =
        ["cambia", "cambiar", "actualiza", "actualizar", "marca", "marcar", "pasa a", "pasar a",
         "mueve", "mover", "avanza", "avanzar", "cierra", "cerrar", "rechaza", "rechazar"];

    private static readonly string[] SenalesEstadisticas =
        ["cuantas", "cuántas", "cuantos", "cuántos", "estadistica", "estadística", "estadisticas",
         "estadísticas", "tendencia", "tendencias", "promedio", "resumen", "indicador", "indicadores",
         "total", "distribucion", "distribución", "panorama", "situacion", "situación", "rezagadas"];

    private static bool EsModoAgente(IReadOnlyList<MensajeLenguaje> mensajes) =>
        mensajes.Any(m => m.Rol == RolMensaje.Sistema
                          && m.Contenido.Contains(MarcaCatalogo, StringComparison.Ordinal));

    private static string ResponderComoAgente(IReadOnlyList<MensajeLenguaje> mensajes)
    {
        var instruccion = mensajes.FirstOrDefault(m => m.Rol == RolMensaje.Sistema).Contenido ?? string.Empty;

        var mensajesUsuario = mensajes
            .Where(m => m.Rol == RolMensaje.Usuario)
            .Select(m => m.Contenido)
            .ToArray();

        if (mensajesUsuario.Length == 0)
        {
            return Responder("No se recibió ninguna consulta.");
        }

        var pregunta = mensajesUsuario[0];
        var ultimo = mensajesUsuario[^1];

        // Ya se ejecutó una herramienta: con su resultado se cierra la respuesta.
        if (ultimo.StartsWith(MarcaResultado, StringComparison.Ordinal))
        {
            return Responder(RedactarDesdeResultado(ultimo));
        }

        // La acción quedó retenida o la herramienta no existía: se responde con
        // lo que se sabe en lugar de insistir.
        if (ultimo.Contains("pendiente de aprobación humana", StringComparison.OrdinalIgnoreCase))
        {
            return Responder(
                "La acción solicitada modifica datos y quedó pendiente de aprobación de un operador. " +
                "No se realizó ningún cambio.");
        }

        if (ultimo.Contains("no existe. Herramientas disponibles", StringComparison.OrdinalIgnoreCase))
        {
            return Responder("No dispongo de una herramienta adecuada para resolver esa consulta.");
        }

        return ElegirHerramienta(pregunta, instruccion);
    }

    private static string ElegirHerramienta(string pregunta, string instruccion)
    {
        var normalizada = Normalizar(pregunta);
        var terminos = pregunta.ToLowerInvariant();

        var codigo = CodigoSolicitud().Match(pregunta);

        // Intención explícita de modificar el estado de una solicitud.
        //
        // La propuesta se emite aunque las instrucciones digan que la
        // herramienta no está autorizada. Es intencional: reproduce el
        // comportamiento de un modelo real, que puede ignorar una instrucción,
        // y deja en evidencia que la barrera efectiva no es el texto del prompt
        // sino el control que aplica el agente antes de ejecutar.
        if (codigo.Success && EnCatalogo(instruccion, "cambiar_estado_solicitud"))
        {
            var estado = DetectarEstado(normalizada);

            if (estado is not null && SenalesCambio.Any(s => terminos.Contains(s, StringComparison.Ordinal)))
            {
                return Invocar("cambiar_estado_solicitud", new
                {
                    codigo = codigo.Value.ToUpperInvariant(),
                    nuevoEstado = estado,
                    motivo = "Cambio solicitado a través del agente."
                });
            }
        }

        // Un código sin intención de cambio significa que se pide el expediente.
        if (codigo.Success && Disponible(instruccion, "ver_solicitud"))
        {
            return Invocar("ver_solicitud", new { codigo = codigo.Value.ToUpperInvariant() });
        }

        if (SenalesReglamento.Any(s => terminos.Contains(s, StringComparison.Ordinal))
            && Disponible(instruccion, "consultar_reglamento"))
        {
            return Invocar("consultar_reglamento", new { pregunta });
        }

        if (SenalesEstadisticas.Any(s => terminos.Contains(s, StringComparison.Ordinal))
            && Disponible(instruccion, "obtener_estadisticas"))
        {
            return Invocar("obtener_estadisticas", new { });
        }

        if (Disponible(instruccion, "buscar_solicitudes"))
        {
            return Invocar("buscar_solicitudes", ArmarFiltros(normalizada));
        }

        return Responder("No dispongo de herramientas para responder esa consulta.");
    }

    /// <summary>
    /// Reconoce el nombre de un estado dentro del texto normalizado.
    /// </summary>
    private static string? DetectarEstado(string normalizada) => normalizada switch
    {
        var t when t.Contains("enrevision") || t.Contains("revision") => "EnRevision",
        var t when t.Contains("enproceso") || t.Contains("proceso") => "EnProceso",
        var t when t.Contains("resuelta") => "Resuelta",
        var t when t.Contains("cerrada") => "Cerrada",
        var t when t.Contains("rechazada") => "Rechazada",
        var t when t.Contains("recibida") => "Recibida",
        _ => null
    };

    /// <summary>
    /// Deduce los filtros de búsqueda a partir de los términos de la pregunta.
    /// </summary>
    private static object ArmarFiltros(string normalizada)
    {
        var estado = DetectarEstado(normalizada);

        string? categoria = normalizada switch
        {
            var t when t.Contains("tecnolog") || t.Contains("sistema") => "Tecnologica",
            var t when t.Contains("academ") => "Academica",
            var t when t.Contains("administrativ") => "Administrativa",
            var t when t.Contains("financ") || t.Contains("pago") => "Financiera",
            var t when t.Contains("infraestructura") || t.Contains("aula") => "Infraestructura",
            _ => null
        };

        string? prioridad = normalizada switch
        {
            var t when t.Contains("critica") => "Critica",
            var t when t.Contains("alta") => "Alta",
            var t when t.Contains("media") => "Media",
            var t when t.Contains("baja") => "Baja",
            _ => null
        };

        return new { estado, categoria, prioridad, cantidad = 10 };
    }

    /// <summary>
    /// Indica si la herramienta figura en el catálogo entregado.
    /// </summary>
    private static bool EnCatalogo(string instruccion, string herramienta) =>
        instruccion.Contains($"- {herramienta}:", StringComparison.Ordinal);

    /// <summary>
    /// Indica si la herramienta figura en el catálogo y además no está marcada
    /// como no autorizada para esta consulta.
    /// </summary>
    private static bool Disponible(string instruccion, string herramienta)
    {
        var posicion = instruccion.IndexOf($"- {herramienta}:", StringComparison.Ordinal);

        if (posicion < 0)
        {
            return false;
        }

        var siguiente = instruccion.IndexOf("\n- ", posicion + 1, StringComparison.Ordinal);
        var bloque = siguiente > posicion
            ? instruccion[posicion..siguiente]
            : instruccion[posicion..];

        return !bloque.Contains("NO está autorizada", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Convierte el resultado de una herramienta en la respuesta final,
    /// conservando el contenido tal como lo devolvió y sin agregarle nada.
    /// </summary>
    private static string RedactarDesdeResultado(string mensaje)
    {
        var salto = mensaje.IndexOf('\n');

        return salto > 0 && salto + 1 < mensaje.Length
            ? mensaje[(salto + 1)..].Trim()
            : mensaje.Trim();
    }

    private static string Invocar(string herramienta, object argumentos) =>
        JsonSerializer.Serialize(
            new { accion = "herramienta", herramienta, argumentos },
            OpcionesJson.Predeterminadas);

    private static string Responder(string respuesta) =>
        JsonSerializer.Serialize(
            new { accion = "responder", respuesta },
            OpcionesJson.Predeterminadas);
}
