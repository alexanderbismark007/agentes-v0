using MesaAyuda.Api.Dominio.Auditoria;
using MesaAyuda.Api.Dominio.Solicitudes;

namespace MesaAyuda.Api.Modulos.Orquestacion.Contratos;

/// <summary>
/// Solicitud que llega desde un formulario o un flujo automatizado.
///
/// Es deliberadamente más simple que el contrato interno: quien envía desde un
/// formulario web no conoce categorías ni unidades responsables. Eso lo resuelve
/// el sistema.
/// </summary>
public sealed record SolicitudEntrante
{
    public string Titulo { get; init; } = string.Empty;

    public string Descripcion { get; init; } = string.Empty;

    public string SolicitanteNombre { get; init; } = string.Empty;

    public string SolicitanteCorreo { get; init; } = string.Empty;

    /// <summary>Identificador del envío en el sistema de origen, si lo tiene.</summary>
    public string? ReferenciaExterna { get; init; }

    /// <summary>Sistema que envía el evento. Se registra en la bitácora.</summary>
    public string Origen { get; init; } = "externo";
}

/// <summary>
/// Resultado del procesamiento de una solicitud entrante.
///
/// Contiene todo lo que el flujo de automatización necesita para su paso
/// siguiente: el código asignado, la clasificación, la unidad responsable y la
/// respuesta automática que puede enviarse por correo.
/// </summary>
public sealed record ResultadoIngreso
{
    public required Guid EventoId { get; init; }

    public required string Codigo { get; init; }

    public required Guid SolicitudId { get; init; }

    public required string Categoria { get; init; }

    public required string Prioridad { get; init; }

    public required string UnidadDestino { get; init; }

    public required string CorreoDestinatario { get; init; }

    /// <summary>Asunto sugerido para el acuse de recibo.</summary>
    public required string AsuntoRespuesta { get; init; }

    /// <summary>
    /// Cuerpo del acuse de recibo. Cuando el reglamento contiene información
    /// pertinente, incluye la respuesta y las fuentes que la respaldan.
    /// </summary>
    public required string CuerpoRespuesta { get; init; }

    /// <summary>
    /// Indica si la respuesta se apoya en el reglamento. Cuando es falso, el
    /// acuse solo confirma la recepción y no afirma nada sobre el fondo.
    /// </summary>
    public required bool RespuestaConRespaldo { get; init; }

    /// <summary>Verdadero si el evento ya se había procesado antes.</summary>
    public required bool Duplicado { get; init; }

    public required int MilisegundosTotales { get; init; }
}

/// <summary>
/// Evento registrado en la bitácora, tal como lo devuelve la API.
/// </summary>
public sealed record EventoRespuesta
{
    public required Guid Id { get; init; }

    public required string Tipo { get; init; }

    public required string Origen { get; init; }

    public required string Resultado { get; init; }

    public string? Referencia { get; init; }

    public string? Detalle { get; init; }

    public required int Milisegundos { get; init; }

    public required int Intentos { get; init; }

    public required DateTimeOffset FechaCreacion { get; init; }

    public static EventoRespuesta Desde(EventoAuditoria evento) => new()
    {
        Id = evento.Id,
        Tipo = evento.Tipo,
        Origen = evento.Origen,
        Resultado = evento.Resultado.ToString(),
        Referencia = evento.Referencia,
        Detalle = evento.Detalle,
        Milisegundos = evento.Milisegundos,
        Intentos = evento.Intentos,
        FechaCreacion = evento.FechaCreacion
    };
}

/// <summary>
/// Filtros del listado de la bitácora.
/// </summary>
public sealed record FiltroEventos
{
    public ResultadoEvento? Resultado { get; init; }

    public string? Origen { get; init; }

    public int Pagina { get; init; } = 1;

    public int TamanoPagina { get; init; } = 20;
}

/// <summary>
/// Resumen operativo de la automatización, para vigilar que el flujo funcione.
/// </summary>
public sealed record SaludOrquestacion
{
    public required int TotalEventos { get; init; }

    public required int Procesados { get; init; }

    public required int Fallidos { get; init; }

    public required int Duplicados { get; init; }

    public required int Rechazados { get; init; }

    /// <summary>Porcentaje de eventos procesados sin error.</summary>
    public required double TasaExito { get; init; }

    public required double MilisegundosPromedio { get; init; }

    public required bool FirmaExigida { get; init; }

    public DateTimeOffset? UltimoEvento { get; init; }
}

/// <summary>
/// Categorías conocidas, expuestas para que el flujo externo pueda mostrarlas.
/// </summary>
public static class CategoriasConocidas
{
    public static IReadOnlyCollection<string> Nombres => Enum.GetNames<CategoriaSolicitud>();
}
