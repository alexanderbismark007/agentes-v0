using MesaAyuda.Api.Dominio.Comun;
using MesaAyuda.Api.Nucleo.Errores;

namespace MesaAyuda.Api.Dominio.Solicitudes;

/// <summary>
/// Solicitud presentada por un miembro de la comunidad universitaria.
/// Concentra las reglas de su ciclo de vida para que ningún servicio externo
/// pueda dejarla en un estado inconsistente.
/// </summary>
public class Solicitud : EntidadBase
{
    private readonly List<ComentarioSolicitud> _comentarios = [];

    private Solicitud()
    {
        Codigo = string.Empty;
        Titulo = string.Empty;
        Descripcion = string.Empty;
        SolicitanteNombre = string.Empty;
        SolicitanteCorreo = string.Empty;
        UnidadDestino = string.Empty;
    }

    public Solicitud(
        string codigo,
        string titulo,
        string descripcion,
        string solicitanteNombre,
        string solicitanteCorreo,
        string unidadDestino,
        CategoriaSolicitud categoria,
        PrioridadSolicitud prioridad)
    {
        Codigo = codigo;
        Titulo = titulo.Trim();
        Descripcion = descripcion.Trim();
        SolicitanteNombre = solicitanteNombre.Trim();
        SolicitanteCorreo = solicitanteCorreo.Trim().ToLowerInvariant();
        UnidadDestino = unidadDestino.Trim();
        Categoria = categoria;
        Prioridad = prioridad;
        Estado = EstadoSolicitud.Recibida;
    }

    /// <summary>Identificador legible para el solicitante, por ejemplo SOL-2026-000042.</summary>
    public string Codigo { get; private set; }

    public string Titulo { get; private set; }

    public string Descripcion { get; private set; }

    public string SolicitanteNombre { get; private set; }

    public string SolicitanteCorreo { get; private set; }

    public string UnidadDestino { get; private set; }

    public CategoriaSolicitud Categoria { get; private set; }

    public PrioridadSolicitud Prioridad { get; private set; }

    public EstadoSolicitud Estado { get; private set; }

    /// <summary>Categoría sugerida por el clasificador al registrar la solicitud.</summary>
    public CategoriaSolicitud? CategoriaSugerida { get; private set; }

    /// <summary>Confianza de la sugerencia, entre 0 y 1.</summary>
    public double? ConfianzaSugerencia { get; private set; }

    /// <summary>Nombre del componente que produjo la sugerencia, para trazabilidad.</summary>
    public string? OrigenSugerencia { get; private set; }

    public DateTimeOffset? FechaCierre { get; private set; }

    public IReadOnlyCollection<ComentarioSolicitud> Comentarios => _comentarios.AsReadOnly();

    /// <summary>
    /// Registra la sugerencia del clasificador sin sobrescribir la decisión humana.
    /// </summary>
    public void RegistrarSugerencia(CategoriaSolicitud categoria, double confianza, string origen)
    {
        CategoriaSugerida = categoria;
        ConfianzaSugerencia = Math.Clamp(confianza, 0d, 1d);
        OrigenSugerencia = origen;
        MarcarActualizacion();
    }

    /// <summary>
    /// Cambia el estado validando la transición contra la máquina de estados.
    /// </summary>
    public void CambiarEstado(EstadoSolicitud nuevoEstado)
    {
        if (nuevoEstado == Estado)
        {
            throw new ExcepcionConflicto($"La solicitud ya se encuentra en estado '{Estado}'.");
        }

        if (!MaquinaEstados.EsTransicionValida(Estado, nuevoEstado))
        {
            var permitidos = MaquinaEstados.EstadosPermitidosDesde(Estado);
            var detalle = permitidos.Count == 0
                ? "es un estado final y no admite más cambios"
                : $"solo permite avanzar a: {string.Join(", ", permitidos)}";

            throw new ExcepcionConflicto($"No se puede pasar de '{Estado}' a '{nuevoEstado}'. El estado actual {detalle}.");
        }

        Estado = nuevoEstado;
        FechaCierre = MaquinaEstados.EsEstadoFinal(nuevoEstado) ? DateTimeOffset.UtcNow : null;
        MarcarActualizacion();
    }

    /// <summary>
    /// Reasigna categoría, prioridad y unidad responsable de la solicitud.
    /// </summary>
    public void Reasignar(CategoriaSolicitud categoria, PrioridadSolicitud prioridad, string unidadDestino)
    {
        if (MaquinaEstados.EsEstadoFinal(Estado))
        {
            throw new ExcepcionConflicto($"No se puede reasignar una solicitud en estado '{Estado}'.");
        }

        Categoria = categoria;
        Prioridad = prioridad;
        UnidadDestino = unidadDestino.Trim();
        MarcarActualizacion();
    }

    /// <summary>
    /// Agrega un comentario al expediente de la solicitud.
    /// </summary>
    public ComentarioSolicitud AgregarComentario(string autor, string contenido, bool esInterno)
    {
        if (Estado == EstadoSolicitud.Cerrada)
        {
            throw new ExcepcionConflicto("No se pueden agregar comentarios a una solicitud cerrada.");
        }

        var comentario = new ComentarioSolicitud(Id, autor, contenido, esInterno);
        _comentarios.Add(comentario);
        MarcarActualizacion();
        return comentario;
    }
}
