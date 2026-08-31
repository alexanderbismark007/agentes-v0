namespace MesaAyuda.Api.Dominio.Solicitudes;

/// <summary>
/// Etapas por las que atraviesa una solicitud dentro de la mesa de ayuda.
/// </summary>
public enum EstadoSolicitud
{
    Recibida = 1,
    EnRevision = 2,
    EnProceso = 3,
    Resuelta = 4,
    Cerrada = 5,
    Rechazada = 6
}
