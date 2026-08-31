namespace MesaAyuda.Api.Dominio.Solicitudes;

/// <summary>
/// Define qué transiciones de estado son legítimas. Mantener esta regla en el
/// dominio evita que cada endpoint invente su propia versión del flujo.
/// </summary>
public static class MaquinaEstados
{
    private static readonly IReadOnlyDictionary<EstadoSolicitud, EstadoSolicitud[]> Transiciones =
        new Dictionary<EstadoSolicitud, EstadoSolicitud[]>
        {
            [EstadoSolicitud.Recibida] = [EstadoSolicitud.EnRevision, EstadoSolicitud.Rechazada],
            [EstadoSolicitud.EnRevision] = [EstadoSolicitud.EnProceso, EstadoSolicitud.Rechazada],
            [EstadoSolicitud.EnProceso] = [EstadoSolicitud.Resuelta, EstadoSolicitud.Rechazada],
            [EstadoSolicitud.Resuelta] = [EstadoSolicitud.Cerrada, EstadoSolicitud.EnProceso],
            [EstadoSolicitud.Cerrada] = [],
            [EstadoSolicitud.Rechazada] = []
        };

    /// <summary>
    /// Indica si se puede pasar de <paramref name="origen"/> a <paramref name="destino"/>.
    /// </summary>
    public static bool EsTransicionValida(EstadoSolicitud origen, EstadoSolicitud destino) =>
        Transiciones.TryGetValue(origen, out var permitidos) && permitidos.Contains(destino);

    /// <summary>
    /// Devuelve los estados alcanzables desde el estado indicado.
    /// </summary>
    public static IReadOnlyCollection<EstadoSolicitud> EstadosPermitidosDesde(EstadoSolicitud origen) =>
        Transiciones.TryGetValue(origen, out var permitidos) ? permitidos : [];

    /// <summary>
    /// Estados en los que la solicitud ya no admite trabajo adicional.
    /// </summary>
    public static bool EsEstadoFinal(EstadoSolicitud estado) =>
        estado is EstadoSolicitud.Cerrada or EstadoSolicitud.Rechazada;
}
