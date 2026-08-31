using FluentValidation;
using MesaAyuda.Api.Dominio.Conocimiento;
using MesaAyuda.Api.Modulos.Conocimiento.Contratos;
using MesaAyuda.Api.Nucleo.Errores;

namespace MesaAyuda.Api.Modulos.Conocimiento;

/// <summary>
/// Endpoints del módulo de conocimiento documental.
/// </summary>
public static class ConocimientoEndpoints
{
    /// <summary>Tamaño máximo aceptado para un documento cargado.</summary>
    private const long TamanoMaximoBytes = 20 * 1024 * 1024;

    public static IEndpointRouteBuilder MapearConocimiento(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/v1/conocimiento")
            .WithTags("Conocimiento");

        grupo.MapPost("/consultas", async (
                ConsultaDocumental peticion,
                IValidator<ConsultaDocumental> validador,
                ServicioConsultas servicio,
                CancellationToken cancelacion) =>
            {
                var invalido = await validador.ValidarAsync(peticion, cancelacion);
                if (invalido is not null)
                {
                    return invalido;
                }

                var respuesta = await servicio.ResponderAsync(peticion.Pregunta, peticion.NivelAcceso, cancelacion);
                return Results.Ok(respuesta);
            })
            .WithName("ConsultarDocumentos")
            .WithSummary("Responde una pregunta sobre los documentos indexados")
            .WithDescription(
                "La respuesta se construye únicamente con los fragmentos recuperados y devuelve las fuentes " +
                "que la respaldan. Si no hay fragmentos pertinentes, se informa que no hay información " +
                "en lugar de improvisar una respuesta.")
            .Produces<RespuestaConsulta>()
            .ProducesValidationProblem();

        grupo.MapPost("/documentos", async (
                IFormFile archivo,
                string? titulo,
                NivelAcceso? nivelAcceso,
                ServicioIndexacion servicio,
                CancellationToken cancelacion) =>
            {
                if (archivo.Length == 0)
                {
                    throw new ExcepcionDominio("El archivo enviado está vacío.");
                }

                if (archivo.Length > TamanoMaximoBytes)
                {
                    throw new ExcepcionDominio(
                        $"El archivo supera el tamaño máximo permitido de {TamanoMaximoBytes / (1024 * 1024)} MB.");
                }

                await using var contenido = archivo.OpenReadStream();

                var resultado = await servicio.IndexarAsync(
                    contenido,
                    archivo.FileName,
                    titulo,
                    nivelAcceso ?? NivelAcceso.Publico,
                    cancelacion);

                return Results.Ok(resultado);
            })
            .WithName("IndexarDocumento")
            .WithSummary("Carga e indexa un documento institucional")
            .WithDescription("Formatos admitidos: PDF, TXT y MD. Un archivo ya indexado y sin cambios no se reprocesa.")
            .DisableAntiforgery()
            .Produces<ResultadoIndexacion>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        grupo.MapGet("/documentos", async (
                ServicioIndexacion servicio,
                CancellationToken cancelacion) =>
                Results.Ok(await servicio.ListarAsync(cancelacion)))
            .WithName("ListarDocumentos")
            .WithSummary("Lista los documentos de la base de conocimiento")
            .Produces<IReadOnlyCollection<DocumentoRespuesta>>();

        grupo.MapDelete("/documentos/{id:guid}", async (
                Guid id,
                ServicioIndexacion servicio,
                CancellationToken cancelacion) =>
            {
                await servicio.EliminarAsync(id, cancelacion);
                return Results.NoContent();
            })
            .WithName("EliminarDocumento")
            .WithSummary("Elimina un documento y todos sus fragmentos")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return rutas;
    }
}
