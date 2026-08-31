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
    /// El valor por defecto es "simulado" para que el proyecto funcione
    /// sin credenciales ni conexión a Internet.
    /// </summary>
    public string Lenguaje { get; set; } = "simulado";

    public OpcionesNube Nube { get; set; } = new();
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

    /// <summary>Segundos de espera máxima por respuesta antes de abortar.</summary>
    public int TiempoEsperaSegundos { get; set; } = 60;
}
