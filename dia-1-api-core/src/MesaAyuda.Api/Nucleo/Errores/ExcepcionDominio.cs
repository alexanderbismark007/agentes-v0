namespace MesaAyuda.Api.Nucleo.Errores;

/// <summary>
/// Error previsible provocado por una regla de negocio. Se traduce a una
/// respuesta HTTP con el código indicado, sin exponer detalles internos.
/// </summary>
public class ExcepcionDominio : Exception
{
    public ExcepcionDominio(string mensaje, int codigoHttp = StatusCodes.Status400BadRequest)
        : base(mensaje)
    {
        CodigoHttp = codigoHttp;
    }

    public int CodigoHttp { get; }
}

/// <summary>
/// El recurso solicitado no existe o el usuario no tiene visibilidad sobre él.
/// </summary>
public sealed class ExcepcionNoEncontrado(string recurso, object identificador)
    : ExcepcionDominio($"No se encontró {recurso} con identificador '{identificador}'.", StatusCodes.Status404NotFound);

/// <summary>
/// La operación es válida en general, pero no en el estado actual del recurso.
/// </summary>
public sealed class ExcepcionConflicto(string mensaje)
    : ExcepcionDominio(mensaje, StatusCodes.Status409Conflict);
