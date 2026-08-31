using MesaAyuda.Api.Dominio.Solicitudes;
using MesaAyuda.Api.Nucleo.Datos;
using Microsoft.EntityFrameworkCore;

namespace MesaAyuda.Api.Modulos.Solicitudes;

/// <summary>
/// Conteo de solicitudes agrupadas por un criterio.
/// </summary>
/// <param name="Clave">Valor del criterio, por ejemplo "EnProceso".</param>
/// <param name="Cantidad">Número de solicitudes con ese valor.</param>
public sealed record ConteoAgrupado(string Clave, int Cantidad);

/// <summary>
/// Fotografía del estado de la mesa de ayuda en un momento dado.
/// </summary>
public sealed record ResumenOperativo
{
    public required int Total { get; init; }

    public required int Abiertas { get; init; }

    public required int Cerradas { get; init; }

    /// <summary>Solicitudes sin resolver con más de tres días de antigüedad.</summary>
    public required int Rezagadas { get; init; }

    public required double HorasPromedioResolucion { get; init; }

    public required IReadOnlyCollection<ConteoAgrupado> PorEstado { get; init; }

    public required IReadOnlyCollection<ConteoAgrupado> PorCategoria { get; init; }

    public required IReadOnlyCollection<ConteoAgrupado> PorPrioridad { get; init; }

    public required DateTimeOffset GeneradoEn { get; init; }
}

/// <summary>
/// Calcula los indicadores operativos de la mesa de ayuda. Se mantiene separado
/// de <see cref="ServicioSolicitudes"/> porque responde a una pregunta distinta:
/// aquel atiende casos individuales, este describe el conjunto.
/// </summary>
public sealed class ServicioEstadisticas(ContextoMesaAyuda contexto)
{
    private const int DiasParaConsiderarRezagada = 3;

    public async Task<ResumenOperativo> ResumirAsync(CancellationToken cancelacion = default)
    {
        var ahora = DateTimeOffset.UtcNow;
        var limiteRezago = ahora.AddDays(-DiasParaConsiderarRezagada);

        var solicitudes = await contexto.Solicitudes
            .AsNoTracking()
            .Select(s => new
            {
                s.Estado,
                s.Categoria,
                s.Prioridad,
                s.FechaCreacion,
                s.FechaCierre
            })
            .ToListAsync(cancelacion);

        var estadosCerrados = new[] { EstadoSolicitud.Cerrada, EstadoSolicitud.Rechazada };

        var resueltas = solicitudes
            .Where(s => s.FechaCierre.HasValue)
            .Select(s => (s.FechaCierre!.Value - s.FechaCreacion).TotalHours)
            .ToList();

        return new ResumenOperativo
        {
            Total = solicitudes.Count,
            Abiertas = solicitudes.Count(s => !estadosCerrados.Contains(s.Estado)),
            Cerradas = solicitudes.Count(s => estadosCerrados.Contains(s.Estado)),
            Rezagadas = solicitudes.Count(s =>
                !estadosCerrados.Contains(s.Estado) && s.FechaCreacion < limiteRezago),
            HorasPromedioResolucion = resueltas.Count > 0 ? Math.Round(resueltas.Average(), 1) : 0d,
            PorEstado = Agrupar(solicitudes.Select(s => s.Estado.ToString())),
            PorCategoria = Agrupar(solicitudes.Select(s => s.Categoria.ToString())),
            PorPrioridad = Agrupar(solicitudes.Select(s => s.Prioridad.ToString())),
            GeneradoEn = ahora
        };
    }

    private static IReadOnlyCollection<ConteoAgrupado> Agrupar(IEnumerable<string> valores) =>
        valores
            .GroupBy(v => v)
            .Select(g => new ConteoAgrupado(g.Key, g.Count()))
            .OrderByDescending(c => c.Cantidad)
            .ThenBy(c => c.Clave, StringComparer.Ordinal)
            .ToArray();
}
