using MesaAyuda.Api.Dominio.Comun;

namespace MesaAyuda.Api.Dominio.Conocimiento;

/// <summary>
/// Nivel de acceso de un documento. Determina quién puede ver los fragmentos
/// que se recuperan de él.
/// </summary>
public enum NivelAcceso
{
    /// <summary>Visible para cualquier miembro de la comunidad universitaria.</summary>
    Publico = 1,

    /// <summary>Visible solo para el personal de la mesa de ayuda.</summary>
    Interno = 2
}

/// <summary>
/// Documento institucional incorporado a la base de conocimiento.
/// </summary>
public class Documento : EntidadBase
{
    private readonly List<FragmentoDocumento> _fragmentos = [];

    private Documento()
    {
        Titulo = string.Empty;
        NombreArchivo = string.Empty;
        HuellaContenido = string.Empty;
    }

    public Documento(string titulo, string nombreArchivo, string huellaContenido, NivelAcceso nivelAcceso)
    {
        Titulo = titulo.Trim();
        NombreArchivo = nombreArchivo.Trim();
        HuellaContenido = huellaContenido;
        NivelAcceso = nivelAcceso;
    }

    public string Titulo { get; private set; }

    public string NombreArchivo { get; private set; }

    /// <summary>
    /// Resumen criptográfico del contenido. Permite detectar si un documento ya
    /// indexado cambió, y así evitar reindexar lo que no se modificó.
    /// </summary>
    public string HuellaContenido { get; private set; }

    public NivelAcceso NivelAcceso { get; private set; }

    public int CantidadFragmentos { get; private set; }

    /// <summary>Modelo con el que se generaron los vectores de este documento.</summary>
    public string? ModeloEmbeddings { get; private set; }

    public IReadOnlyCollection<FragmentoDocumento> Fragmentos => _fragmentos.AsReadOnly();

    /// <summary>
    /// Reemplaza por completo los fragmentos del documento. La indexación no es
    /// incremental: si el contenido cambió, los fragmentos anteriores dejan de
    /// ser válidos y se descartan en bloque.
    /// </summary>
    public void ReemplazarFragmentos(IEnumerable<FragmentoDocumento> fragmentos, string modeloEmbeddings)
    {
        _fragmentos.Clear();
        _fragmentos.AddRange(fragmentos);

        CantidadFragmentos = _fragmentos.Count;
        ModeloEmbeddings = modeloEmbeddings;
        MarcarActualizacion();
    }

    public void ActualizarMetadatos(string titulo, NivelAcceso nivelAcceso, string huellaContenido)
    {
        Titulo = titulo.Trim();
        NivelAcceso = nivelAcceso;
        HuellaContenido = huellaContenido;
        MarcarActualizacion();
    }
}
