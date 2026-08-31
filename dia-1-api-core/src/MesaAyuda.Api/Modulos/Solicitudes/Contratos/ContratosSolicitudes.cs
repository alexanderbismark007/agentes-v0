using MesaAyuda.Api.Dominio.Solicitudes;

namespace MesaAyuda.Api.Modulos.Solicitudes.Contratos;

/// <summary>
/// Datos necesarios para registrar una nueva solicitud.
/// </summary>
public sealed record CrearSolicitud
{
    public string Titulo { get; init; } = string.Empty;

    public string Descripcion { get; init; } = string.Empty;

    public string SolicitanteNombre { get; init; } = string.Empty;

    public string SolicitanteCorreo { get; init; } = string.Empty;

    /// <summary>Unidad responsable. Si se omite, se deduce de la categoría.</summary>
    public string? UnidadDestino { get; init; }

    /// <summary>Categoría declarada por el solicitante. Si se omite, se usa la sugerida.</summary>
    public CategoriaSolicitud? Categoria { get; init; }

    public PrioridadSolicitud Prioridad { get; init; } = PrioridadSolicitud.Media;
}

/// <summary>
/// Cambio de estado solicitado sobre una solicitud existente.
/// </summary>
public sealed record CambiarEstado
{
    public EstadoSolicitud NuevoEstado { get; init; }

    public string? Motivo { get; init; }
}

/// <summary>
/// Reasignación de categoría, prioridad y unidad responsable.
/// </summary>
public sealed record ReasignarSolicitud
{
    public CategoriaSolicitud Categoria { get; init; }

    public PrioridadSolicitud Prioridad { get; init; }

    public string UnidadDestino { get; init; } = string.Empty;
}

/// <summary>
/// Comentario que se agrega al expediente de una solicitud.
/// </summary>
public sealed record CrearComentario
{
    public string Autor { get; init; } = string.Empty;

    public string Contenido { get; init; } = string.Empty;

    public bool EsInterno { get; init; }
}

/// <summary>
/// Filtros admitidos por el listado de solicitudes.
/// </summary>
public sealed record FiltroSolicitudes
{
    public EstadoSolicitud? Estado { get; init; }

    public CategoriaSolicitud? Categoria { get; init; }

    public PrioridadSolicitud? Prioridad { get; init; }

    /// <summary>Texto libre que se busca en título y descripción.</summary>
    public string? Texto { get; init; }

    public int Pagina { get; init; } = 1;

    public int TamanoPagina { get; init; } = 20;
}

/// <summary>
/// Representación de una solicitud devuelta por la API.
/// </summary>
public sealed record SolicitudRespuesta
{
    public required Guid Id { get; init; }

    public required string Codigo { get; init; }

    public required string Titulo { get; init; }

    public required string Descripcion { get; init; }

    public required string SolicitanteNombre { get; init; }

    public required string SolicitanteCorreo { get; init; }

    public required string UnidadDestino { get; init; }

    public required string Categoria { get; init; }

    public required string Prioridad { get; init; }

    public required string Estado { get; init; }

    public string? CategoriaSugerida { get; init; }

    public double? ConfianzaSugerencia { get; init; }

    public string? OrigenSugerencia { get; init; }

    public required DateTimeOffset FechaCreacion { get; init; }

    public required DateTimeOffset FechaActualizacion { get; init; }

    public DateTimeOffset? FechaCierre { get; init; }

    /// <summary>Estados a los que la solicitud puede avanzar desde su estado actual.</summary>
    public required IReadOnlyCollection<string> TransicionesPermitidas { get; init; }

    public static SolicitudRespuesta Desde(Solicitud solicitud) => new()
    {
        Id = solicitud.Id,
        Codigo = solicitud.Codigo,
        Titulo = solicitud.Titulo,
        Descripcion = solicitud.Descripcion,
        SolicitanteNombre = solicitud.SolicitanteNombre,
        SolicitanteCorreo = solicitud.SolicitanteCorreo,
        UnidadDestino = solicitud.UnidadDestino,
        Categoria = solicitud.Categoria.ToString(),
        Prioridad = solicitud.Prioridad.ToString(),
        Estado = solicitud.Estado.ToString(),
        CategoriaSugerida = solicitud.CategoriaSugerida?.ToString(),
        ConfianzaSugerencia = solicitud.ConfianzaSugerencia,
        OrigenSugerencia = solicitud.OrigenSugerencia,
        FechaCreacion = solicitud.FechaCreacion,
        FechaActualizacion = solicitud.FechaActualizacion,
        FechaCierre = solicitud.FechaCierre,
        TransicionesPermitidas = MaquinaEstados.EstadosPermitidosDesde(solicitud.Estado)
            .Select(e => e.ToString())
            .ToArray()
    };
}

/// <summary>
/// Comentario devuelto por la API.
/// </summary>
public sealed record ComentarioRespuesta
{
    public required Guid Id { get; init; }

    public required string Autor { get; init; }

    public required string Contenido { get; init; }

    public required bool EsInterno { get; init; }

    public required DateTimeOffset FechaCreacion { get; init; }

    public static ComentarioRespuesta Desde(ComentarioSolicitud comentario) => new()
    {
        Id = comentario.Id,
        Autor = comentario.Autor,
        Contenido = comentario.Contenido,
        EsInterno = comentario.EsInterno,
        FechaCreacion = comentario.FechaCreacion
    };
}

/// <summary>
/// Página de resultados con la información necesaria para paginar en el cliente.
/// </summary>
public sealed record PaginaRespuesta<T>
{
    public required IReadOnlyCollection<T> Elementos { get; init; }

    public required int Pagina { get; init; }

    public required int TamanoPagina { get; init; }

    public required int Total { get; init; }

    public int TotalPaginas => TamanoPagina == 0 ? 0 : (int)Math.Ceiling(Total / (double)TamanoPagina);
}
