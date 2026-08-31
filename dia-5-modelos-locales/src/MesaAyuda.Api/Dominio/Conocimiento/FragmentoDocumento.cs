using MesaAyuda.Api.Dominio.Comun;

namespace MesaAyuda.Api.Dominio.Conocimiento;

/// <summary>
/// Porción de un documento con su vector asociado. Es la unidad que se
/// recupera al responder una consulta: no se devuelve el documento entero,
/// sino los pasajes que efectivamente hablan del tema preguntado.
/// </summary>
public class FragmentoDocumento : EntidadBase
{
    private FragmentoDocumento()
    {
        Contenido = string.Empty;
        Vector = [];
    }

    public FragmentoDocumento(Guid documentoId, int orden, string contenido, float[] vector, string? referencia)
    {
        DocumentoId = documentoId;
        Orden = orden;
        Contenido = contenido.Trim();
        Vector = vector;
        Referencia = referencia;
    }

    public Guid DocumentoId { get; private set; }

    /// <summary>Posición del fragmento dentro del documento, empezando en cero.</summary>
    public int Orden { get; private set; }

    public string Contenido { get; private set; }

    /// <summary>
    /// Ubicación legible dentro del documento, por ejemplo "Artículo 14" o
    /// "Página 3". Es lo que se muestra al citar la fuente.
    /// </summary>
    public string? Referencia { get; private set; }

    /// <summary>Representación vectorial del contenido del fragmento.</summary>
    public float[] Vector { get; private set; }
}
