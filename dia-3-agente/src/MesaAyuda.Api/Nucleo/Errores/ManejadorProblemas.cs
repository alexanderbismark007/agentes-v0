using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MesaAyuda.Api.Nucleo.Errores;

/// <summary>
/// Convierte cualquier excepción no controlada en una respuesta ProblemDetails
/// (RFC 7807), de modo que la API tenga un único formato de error.
/// </summary>
public sealed class ManejadorProblemas(ILogger<ManejadorProblemas> registro) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext contexto,
        Exception excepcion,
        CancellationToken cancelacion)
    {
        var (codigo, titulo, detalle) = excepcion switch
        {
            ExcepcionDominio dominio => (dominio.CodigoHttp, TituloPara(dominio.CodigoHttp), dominio.Message),

            // Un cuerpo mal formado o mal codificado es un error de quien llama,
            // no una falla del servicio: corresponde 400 y no 500.
            BadHttpRequestException solicitudInvalida => (
                StatusCodes.Status400BadRequest,
                "Solicitud inválida",
                "No se pudo interpretar el cuerpo de la petición. " +
                "Verifique que sea JSON válido y esté codificado en UTF-8."),

            _ => (StatusCodes.Status500InternalServerError,
                  "Error interno del servidor",
                  "Ocurrió un error inesperado al procesar la solicitud.")
        };

        if (codigo >= StatusCodes.Status500InternalServerError)
        {
            registro.LogError(excepcion, "Error no controlado en {Ruta}", contexto.Request.Path);
        }
        else
        {
            registro.LogWarning("Regla de negocio rechazó {Ruta}: {Mensaje}", contexto.Request.Path, excepcion.Message);
        }

        var problema = new ProblemDetails
        {
            Status = codigo,
            Title = titulo,
            Detail = detalle,
            Instance = contexto.Request.Path
        };
        problema.Extensions["traceId"] = contexto.TraceIdentifier;

        contexto.Response.StatusCode = codigo;
        await contexto.Response.WriteAsJsonAsync(problema, cancelacion);
        return true;
    }

    private static string TituloPara(int codigo) => codigo switch
    {
        StatusCodes.Status404NotFound => "Recurso no encontrado",
        StatusCodes.Status409Conflict => "Conflicto con el estado actual",
        _ => "Solicitud inválida"
    };
}
