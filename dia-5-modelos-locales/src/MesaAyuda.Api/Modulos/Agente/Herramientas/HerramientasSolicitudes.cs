using System.Text;
using System.Text.Json;
using MesaAyuda.Api.Dominio.Solicitudes;
using MesaAyuda.Api.Modulos.Solicitudes;
using MesaAyuda.Api.Modulos.Solicitudes.Contratos;

namespace MesaAyuda.Api.Modulos.Agente.Herramientas;

/// <summary>
/// Permite al agente consultar la base de solicitudes en lenguaje natural sin
/// exponer la base de datos.
///
/// El agente no escribe SQL: elige valores para un conjunto fijo de filtros que
/// esta herramienta traduce a una consulta segura. Así no existe la posibilidad
/// de una inyección, ni de una consulta que arrase con la tabla completa.
/// </summary>
public sealed class HerramientaBuscarSolicitudes(ServicioSolicitudes servicio) : IHerramientaAgente
{
    /// <summary>Tope de resultados, para que el contexto no se desborde.</summary>
    private const int MaximoResultados = 25;

    public string Nombre => "buscar_solicitudes";

    public string Descripcion =>
        "Busca solicitudes registradas en la mesa de ayuda aplicando filtros. " +
        "Úsala para responder preguntas sobre casos concretos, por ejemplo cuántas solicitudes " +
        "tecnológicas están en proceso o qué solicitudes críticas siguen abiertas. " +
        "Devuelve como máximo 25 resultados.";

    public NivelRiesgo Riesgo => NivelRiesgo.Lectura;

    public object EsquemaParametros => new
    {
        type = "object",
        properties = new
        {
            estado = new
            {
                type = "string",
                description = "Estado de la solicitud.",
                @enum = Enum.GetNames<EstadoSolicitud>()
            },
            categoria = new
            {
                type = "string",
                description = "Área responsable.",
                @enum = Enum.GetNames<CategoriaSolicitud>()
            },
            prioridad = new
            {
                type = "string",
                description = "Urgencia asignada.",
                @enum = Enum.GetNames<PrioridadSolicitud>()
            },
            texto = new
            {
                type = "string",
                description = "Texto a buscar en el título, la descripción o el código."
            },
            cantidad = new
            {
                type = "integer",
                description = "Cantidad máxima de resultados, entre 1 y 25."
            }
        }
    };

    public async Task<ResultadoHerramienta> EjecutarAsync(
        JsonElement argumentos,
        CancellationToken cancelacion = default)
    {
        var filtro = new FiltroSolicitudes
        {
            Estado = Argumentos.Enumeracion<EstadoSolicitud>(argumentos, "estado"),
            Categoria = Argumentos.Enumeracion<CategoriaSolicitud>(argumentos, "categoria"),
            Prioridad = Argumentos.Enumeracion<PrioridadSolicitud>(argumentos, "prioridad"),
            Texto = Argumentos.Texto(argumentos, "texto"),
            Pagina = 1,
            TamanoPagina = Math.Clamp(Argumentos.Entero(argumentos, "cantidad") ?? 10, 1, MaximoResultados)
        };

        var pagina = await servicio.ListarAsync(filtro, cancelacion);

        if (pagina.Total == 0)
        {
            return ResultadoHerramienta.Correcta(
                "No se encontraron solicitudes que cumplan esos criterios.",
                "0 resultados");
        }

        var constructor = new StringBuilder();
        constructor
            .Append("Se encontraron ").Append(pagina.Total)
            .Append(" solicitudes. Se listan ").Append(pagina.Elementos.Count).AppendLine(":");

        foreach (var solicitud in pagina.Elementos)
        {
            constructor
                .Append("- ").Append(solicitud.Codigo)
                .Append(" | ").Append(solicitud.Estado)
                .Append(" | ").Append(solicitud.Categoria)
                .Append(" | prioridad ").Append(solicitud.Prioridad)
                .Append(" | ").Append(solicitud.UnidadDestino)
                .Append(" | ").Append(solicitud.Titulo)
                .Append(" | registrada el ").Append(solicitud.FechaCreacion.ToString("yyyy-MM-dd"))
                .AppendLine();
        }

        return ResultadoHerramienta.Correcta(
            constructor.ToString().TrimEnd(),
            $"{pagina.Total} resultados");
    }
}

/// <summary>
/// Entrega al agente los indicadores agregados de la mesa de ayuda.
///
/// Existe como herramienta propia, y no como un caso de la anterior, porque
/// responde a otra pregunta: "cómo está el conjunto" en vez de "qué casos hay".
/// Calcular esos números recorriendo un listado sería más lento y más frágil.
/// </summary>
public sealed class HerramientaEstadisticas(ServicioEstadisticas servicio) : IHerramientaAgente
{
    public string Nombre => "obtener_estadisticas";

    public string Descripcion =>
        "Devuelve los indicadores operativos de la mesa de ayuda: total de solicitudes, " +
        "abiertas, cerradas, rezagadas, tiempo promedio de resolución y la distribución " +
        "por estado, categoría y prioridad. Úsala para preguntas sobre tendencias, " +
        "volumen o desempeño general, no para casos individuales.";

    public NivelRiesgo Riesgo => NivelRiesgo.Lectura;

    public object EsquemaParametros => new
    {
        type = "object",
        properties = new { }
    };

    public async Task<ResultadoHerramienta> EjecutarAsync(
        JsonElement argumentos,
        CancellationToken cancelacion = default)
    {
        var resumen = await servicio.ResumirAsync(cancelacion);

        var constructor = new StringBuilder();
        constructor.AppendLine("Indicadores operativos de la mesa de ayuda:");
        constructor.Append("- Total de solicitudes: ").AppendLine(resumen.Total.ToString());
        constructor.Append("- Abiertas: ").AppendLine(resumen.Abiertas.ToString());
        constructor.Append("- Cerradas o rechazadas: ").AppendLine(resumen.Cerradas.ToString());
        constructor.Append("- Rezagadas (abiertas con más de tres días): ").AppendLine(resumen.Rezagadas.ToString());
        constructor.Append("- Tiempo promedio de resolución: ").Append(resumen.HorasPromedioResolucion).AppendLine(" horas");

        AgregarDistribucion(constructor, "Por estado", resumen.PorEstado);
        AgregarDistribucion(constructor, "Por categoría", resumen.PorCategoria);
        AgregarDistribucion(constructor, "Por prioridad", resumen.PorPrioridad);

        return ResultadoHerramienta.Correcta(
            constructor.ToString().TrimEnd(),
            $"{resumen.Total} solicitudes analizadas");
    }

    private static void AgregarDistribucion(
        StringBuilder constructor,
        string titulo,
        IReadOnlyCollection<ConteoAgrupado> conteos)
    {
        constructor.Append("- ").Append(titulo).Append(": ");
        constructor.AppendLine(conteos.Count == 0
            ? "sin datos"
            : string.Join(", ", conteos.Select(c => $"{c.Clave} {c.Cantidad}")));
    }
}
