namespace MesaAyuda.Api.Nucleo.Configuracion;

/// <summary>
/// Configuración de los proveedores de modelos. Se enlaza desde la sección
/// "Proveedores" de appsettings.json o desde variables de entorno.
/// </summary>
public sealed class OpcionesProveedores
{
    public const string Seccion = "Proveedores";

    /// <summary>
    /// Proveedor de lenguaje activo. Valores admitidos: "simulado" o "nube".
    /// </summary>
    public string Lenguaje { get; set; } = "simulado";

    /// <summary>
    /// Proveedor de embeddings activo. Valores admitidos: "simulado" o "nube".
    ///
    /// Cambiar este valor invalida los vectores ya almacenados: fueron
    /// generados por otro modelo y no son comparables con los nuevos. Después
    /// de cambiarlo hay que reindexar los documentos.
    /// </summary>
    public string Embeddings { get; set; } = "simulado";

    public OpcionesNube Nube { get; set; } = new();

    public OpcionesLocal Local { get; set; } = new();
}

/// <summary>
/// Datos de conexión a un servicio de modelos compatible con el formato
/// de la API de OpenAI.
/// </summary>
public sealed class OpcionesNube
{
    public string UrlBase { get; set; } = "https://api.openai.com/v1";

    public string ClaveApi { get; set; } = string.Empty;

    public string ModeloLenguaje { get; set; } = "gpt-4o-mini";

    public string ModeloEmbeddings { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Dimensiones que se solicitan al modelo de embeddings. Se mantiene en el
    /// valor estándar del sistema para que los vectores de todos los
    /// proveedores sean intercambiables sin migrar el esquema.
    /// </summary>
    public int DimensionesEmbedding { get; set; } = Dimensiones.Estandar;

    /// <summary>Segundos de espera máxima por respuesta antes de abortar.</summary>
    public int TiempoEsperaSegundos { get; set; } = 60;
}

/// <summary>
/// Datos de conexión al servicio de modelos que corre en la propia
/// infraestructura.
/// </summary>
public sealed class OpcionesLocal
{
    public string UrlBase { get; set; } = "http://localhost:11434";

    public string ModeloLenguaje { get; set; } = "llama3.2:3b";

    /// <summary>
    /// Modelo de embeddings. El valor por defecto produce vectores de 384
    /// dimensiones, que es el tamaño con el que está definida la columna
    /// vectorial de la base de datos.
    /// </summary>
    public string ModeloEmbeddings { get; set; } = "all-minilm";

    /// <summary>
    /// Tokens de contexto que se solicitan al modelo. El valor por defecto de
    /// Ollama es corto y truncaría en silencio los fragmentos recuperados.
    /// </summary>
    public int VentanaContexto { get; set; } = 4096;

    /// <summary>
    /// Espera máxima por respuesta. Es más generosa que en la nube: la primera
    /// consulta debe cargar el modelo en memoria y puede tardar bastante más
    /// que las siguientes.
    /// </summary>
    public int TiempoEsperaSegundos { get; set; } = 180;
}
