using FluentValidation;
using MesaAyuda.Api.Dominio.Solicitudes;
using MesaAyuda.Api.Modulos.Solicitudes.Contratos;
using MesaAyuda.Api.Nucleo.Errores;

namespace MesaAyuda.Api.Modulos.Solicitudes;

/// <summary>
/// Publica los endpoints HTTP del módulo de solicitudes. Cada módulo del
/// sistema expone su propio mapeo, de modo que agregar un módulo nuevo no
/// obliga a modificar el arranque de la aplicación.
/// </summary>
public static class SolicitudesEndpoints
{
    public static IEndpointRouteBuilder MapearSolicitudes(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/v1/solicitudes")
            .WithTags("Solicitudes");

        grupo.MapPost("/", async (
                CrearSolicitud peticion,
                IValidator<CrearSolicitud> validador,
                ServicioSolicitudes servicio,
                CancellationToken cancelacion) =>
            {
                var invalido = await validador.ValidarAsync(peticion, cancelacion);
                if (invalido is not null)
                {
                    return invalido;
                }

                var creada = await servicio.CrearAsync(peticion, cancelacion);
                return Results.Created($"/api/v1/solicitudes/{creada.Id}", creada);
            })
            .WithName("CrearSolicitud")
            .WithSummary("Registra una nueva solicitud")
            .WithDescription("Registra la solicitud, le asigna un código correlativo y guarda la categoría sugerida por el clasificador.")
            .Produces<SolicitudRespuesta>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        grupo.MapGet("/", async (
                EstadoSolicitud? estado,
                CategoriaSolicitud? categoria,
                PrioridadSolicitud? prioridad,
                string? texto,
                int? pagina,
                int? tamanoPagina,
                ServicioSolicitudes servicio,
                CancellationToken cancelacion) =>
            {
                var filtro = new FiltroSolicitudes
                {
                    Estado = estado,
                    Categoria = categoria,
                    Prioridad = prioridad,
                    Texto = texto,
                    Pagina = pagina ?? 1,
                    TamanoPagina = tamanoPagina ?? 20
                };

                return Results.Ok(await servicio.ListarAsync(filtro, cancelacion));
            })
            .WithName("ListarSolicitudes")
            .WithSummary("Lista solicitudes con filtros y paginación")
            .Produces<PaginaRespuesta<SolicitudRespuesta>>();

        grupo.MapGet("/{id:guid}", async (
                Guid id,
                ServicioSolicitudes servicio,
                CancellationToken cancelacion) =>
                Results.Ok(await servicio.ObtenerAsync(id, cancelacion)))
            .WithName("ObtenerSolicitud")
            .WithSummary("Obtiene el detalle de una solicitud")
            .Produces<SolicitudRespuesta>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPatch("/{id:guid}/estado", async (
                Guid id,
                CambiarEstado peticion,
                IValidator<CambiarEstado> validador,
                ServicioSolicitudes servicio,
                CancellationToken cancelacion) =>
            {
                var invalido = await validador.ValidarAsync(peticion, cancelacion);
                if (invalido is not null)
                {
                    return invalido;
                }

                return Results.Ok(await servicio.CambiarEstadoAsync(id, peticion, cancelacion));
            })
            .WithName("CambiarEstadoSolicitud")
            .WithSummary("Avanza la solicitud a un nuevo estado")
            .WithDescription("La transición se valida contra la máquina de estados; una transición no permitida devuelve 409.")
            .Produces<SolicitudRespuesta>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPatch("/{id:guid}/asignacion", async (
                Guid id,
                ReasignarSolicitud peticion,
                IValidator<ReasignarSolicitud> validador,
                ServicioSolicitudes servicio,
                CancellationToken cancelacion) =>
            {
                var invalido = await validador.ValidarAsync(peticion, cancelacion);
                if (invalido is not null)
                {
                    return invalido;
                }

                return Results.Ok(await servicio.ReasignarAsync(id, peticion, cancelacion));
            })
            .WithName("ReasignarSolicitud")
            .WithSummary("Cambia categoría, prioridad y unidad responsable")
            .Produces<SolicitudRespuesta>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPost("/{id:guid}/comentarios", async (
                Guid id,
                CrearComentario peticion,
                IValidator<CrearComentario> validador,
                ServicioSolicitudes servicio,
                CancellationToken cancelacion) =>
            {
                var invalido = await validador.ValidarAsync(peticion, cancelacion);
                if (invalido is not null)
                {
                    return invalido;
                }

                var comentario = await servicio.AgregarComentarioAsync(id, peticion, cancelacion);
                return Results.Created($"/api/v1/solicitudes/{id}/comentarios/{comentario.Id}", comentario);
            })
            .WithName("AgregarComentario")
            .WithSummary("Agrega un comentario al expediente")
            .Produces<ComentarioRespuesta>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapGet("/{id:guid}/comentarios", async (
                Guid id,
                bool? incluirInternos,
                ServicioSolicitudes servicio,
                CancellationToken cancelacion) =>
                Results.Ok(await servicio.ListarComentariosAsync(id, incluirInternos ?? false, cancelacion)))
            .WithName("ListarComentarios")
            .WithSummary("Lista los comentarios de una solicitud")
            .WithDescription("Los comentarios internos solo se incluyen cuando se pide explícitamente.")
            .Produces<IReadOnlyCollection<ComentarioRespuesta>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        rutas.MapGet("/api/v1/estadisticas/resumen", async (
                ServicioEstadisticas servicio,
                CancellationToken cancelacion) =>
                Results.Ok(await servicio.ResumirAsync(cancelacion)))
            .WithTags("Estadísticas")
            .WithName("ResumenOperativo")
            .WithSummary("Devuelve los indicadores operativos de la mesa de ayuda")
            .Produces<ResumenOperativo>();

        return rutas;
    }
}
