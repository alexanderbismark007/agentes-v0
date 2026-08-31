namespace MesaAyuda.Api.Nucleo.Configuracion;

/// <summary>
/// Parámetros del módulo de conocimiento. Se agrupan aquí porque son las
/// perillas que más se ajustan al afinar la calidad de las respuestas.
/// </summary>
public sealed class OpcionesConocimiento
{
    public const string Seccion = "Conocimiento";

    /// <summary>Tamaño objetivo de cada fragmento, en caracteres.</summary>
    public int TamanoFragmento { get; set; } = 900;

    /// <summary>
    /// Caracteres que se repiten entre un fragmento y el siguiente. El
    /// solapamiento evita que una idea partida al medio quede irrecuperable.
    /// </summary>
    public int SolapamientoFragmento { get; set; } = 150;

    /// <summary>Cantidad de fragmentos que se recuperan por consulta.</summary>
    public int FragmentosRecuperados { get; set; } = 5;

    /// <summary>
    /// Similitud mínima para considerar que un fragmento es pertinente. Por
    /// debajo de este valor se descarta, aunque sea de los mejores encontrados.
    /// </summary>
    public double SimilitudMinima { get; set; } = 0.15d;

    /// <summary>Carpeta desde la que se cargan los documentos al iniciar.</summary>
    public string CarpetaDocumentos { get; set; } = "documentos";

    /// <summary>Indica si los documentos de la carpeta se indexan al arrancar.</summary>
    public bool IndexarAlIniciar { get; set; } = true;
}
