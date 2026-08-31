using MesaAyuda.Api.Dominio.Comun;

namespace MesaAyuda.Api.Dominio.Solicitudes;

/// <summary>
/// Anotación registrada sobre una solicitud durante su atención.
/// </summary>
public class ComentarioSolicitud : EntidadBase
{
    private ComentarioSolicitud()
    {
        Autor = string.Empty;
        Contenido = string.Empty;
    }

    public ComentarioSolicitud(Guid solicitudId, string autor, string contenido, bool esInterno)
    {
        SolicitudId = solicitudId;
        Autor = autor.Trim();
        Contenido = contenido.Trim();
        EsInterno = esInterno;
    }

    public Guid SolicitudId { get; private set; }

    public string Autor { get; private set; }

    public string Contenido { get; private set; }

    /// <summary>
    /// Los comentarios internos no se muestran al solicitante.
    /// </summary>
    public bool EsInterno { get; private set; }
}
