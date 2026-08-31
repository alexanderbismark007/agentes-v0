using MesaAyuda.Api.Dominio.Comun;

namespace MesaAyuda.Api.Dominio.Auditoria;

/// <summary>
/// Resultado de un intento de procesar un evento externo.
/// </summary>
public enum ResultadoEvento
{
    /// <summary>Recibido y encolado, aún sin procesar.</summary>
    Recibido = 1,

    /// <summary>Procesado correctamente.</summary>
    Procesado = 2,

    /// <summary>Falló el procesamiento.</summary>
    Fallido = 3,

    /// <summary>Ya se había procesado un evento idéntico; se descartó.</summary>
    Duplicado = 4,

    /// <summary>Se rechazó por firma inválida o datos no admisibles.</summary>
    Rechazado = 5
}

/// <summary>
/// Registro de un evento recibido desde un sistema externo.
///
/// En un proceso automatizado, la bitácora no es un extra: es la única forma de
/// responder qué pasó con una solicitud que entró por un formulario a las tres
/// de la mañana. Se guarda el cuerpo recibido, quién lo envió, qué se decidió y
/// cuánto tardó.
/// </summary>
public class EventoAuditoria : EntidadBase
{
    private EventoAuditoria()
    {
        Tipo = string.Empty;
        Origen = string.Empty;
        Huella = string.Empty;
        Carga = string.Empty;
    }

    public EventoAuditoria(string tipo, string origen, string huella, string carga)
    {
        Tipo = tipo.Trim();
        Origen = origen.Trim();
        Huella = huella;
        Carga = carga;
        Resultado = ResultadoEvento.Recibido;
    }

    /// <summary>Clase de evento, por ejemplo "solicitud.recibida".</summary>
    public string Tipo { get; private set; }

    /// <summary>Sistema que lo envió, por ejemplo "n8n" o "formulario-web".</summary>
    public string Origen { get; private set; }

    /// <summary>
    /// Resumen del contenido recibido. Permite detectar reenvíos del mismo
    /// evento, que en toda automatización ocurren tarde o temprano.
    /// </summary>
    public string Huella { get; private set; }

    /// <summary>Cuerpo recibido, tal como llegó.</summary>
    public string Carga { get; private set; }

    public ResultadoEvento Resultado { get; private set; }

    /// <summary>Identificador del recurso creado o afectado, si lo hubo.</summary>
    public string? Referencia { get; private set; }

    public string? Detalle { get; private set; }

    public int Milisegundos { get; private set; }

    /// <summary>Cantidad de veces que se intentó procesar este evento.</summary>
    public int Intentos { get; private set; }

    public void RegistrarExito(string referencia, int milisegundos)
    {
        Resultado = ResultadoEvento.Procesado;
        Referencia = referencia;
        Milisegundos = milisegundos;
        Intentos++;
        MarcarActualizacion();
    }

    public void RegistrarFallo(string detalle, int milisegundos)
    {
        Resultado = ResultadoEvento.Fallido;
        Detalle = Recortar(detalle);
        Milisegundos = milisegundos;
        Intentos++;
        MarcarActualizacion();
    }

    public void RegistrarDuplicado(string referencia)
    {
        Resultado = ResultadoEvento.Duplicado;
        Referencia = referencia;
        Detalle = "Ya existía un evento con la misma huella de contenido.";
        MarcarActualizacion();
    }

    public void RegistrarRechazo(string detalle)
    {
        Resultado = ResultadoEvento.Rechazado;
        Detalle = Recortar(detalle);
        MarcarActualizacion();
    }

    private static string Recortar(string texto) =>
        texto.Length <= 1000 ? texto : texto[..1000];
}
