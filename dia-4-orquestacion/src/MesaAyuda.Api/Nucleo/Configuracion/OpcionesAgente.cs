namespace MesaAyuda.Api.Nucleo.Configuracion;

/// <summary>
/// Límites operativos del agente. No son detalles de afinación: son la
/// diferencia entre un componente controlado y uno que puede consumir tiempo
/// y dinero sin techo.
/// </summary>
public sealed class OpcionesAgente
{
    public const string Seccion = "Agente";

    /// <summary>
    /// Máximo de ciclos de razonamiento por consulta. Cada ciclo implica una
    /// llamada al modelo, así que este número acota directamente el costo y la
    /// latencia máxima de una respuesta.
    /// </summary>
    public int MaximoIteraciones { get; set; } = 6;

    /// <summary>
    /// Tiempo máximo que puede tardar una herramienta antes de darse por
    /// fallida. Evita que una consulta lenta bloquee todo el razonamiento.
    /// </summary>
    public int TiempoMaximoHerramientaSegundos { get; set; } = 30;
}
