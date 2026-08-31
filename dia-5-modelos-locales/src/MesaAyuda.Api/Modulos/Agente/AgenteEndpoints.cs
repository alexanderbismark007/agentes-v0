using FluentValidation;
using MesaAyuda.Api.Modulos.Agente.Contratos;
using MesaAyuda.Api.Nucleo.Errores;

namespace MesaAyuda.Api.Modulos.Agente;

/// <summary>
/// Endpoints del módulo del agente.
/// </summary>
public static class AgenteEndpoints
{
    public static IEndpointRouteBuilder MapearAgente(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/v1/agente")
            .WithTags("Agente");

        grupo.MapPost("/consultas", async (
                ConsultaAgente peticion,
                IValidator<ConsultaAgente> validador,
                ServicioAgente servicio,
                CancellationToken cancelacion) =>
            {
                var invalido = await validador.ValidarAsync(peticion, cancelacion);
                if (invalido is not null)
                {
                    return invalido;
                }

                return Results.Ok(await servicio.ResponderAsync(peticion, cancelacion));
            })
            .WithName("ConsultarAgente")
            .WithSummary("Resuelve una consulta eligiendo y ejecutando herramientas")
            .WithDescription(
                "El agente decide qué herramientas usar según la pregunta y devuelve la traza completa " +
                "de su razonamiento. Las herramientas que modifican datos solo se ejecutan si la consulta " +
                "incluye la autorización explícita del operador; de lo contrario quedan como acciones pendientes.")
            .Produces<RespuestaAgente>()
            .ProducesValidationProblem();

        grupo.MapGet("/herramientas", (ServicioAgente servicio) =>
                Results.Ok(servicio.Catalogo()))
            .WithName("ListarHerramientas")
            .WithSummary("Lista las herramientas que el agente puede utilizar")
            .WithDescription("El conjunto es cerrado: el agente no puede ejecutar nada que no esté aquí.")
            .Produces<IReadOnlyCollection<HerramientaDisponible>>();

        grupo.MapPost("/reportes/ejecutivo", async (
                ServicioReporte servicio,
                CancellationToken cancelacion) =>
                Results.Ok(await servicio.GenerarAsync(cancelacion)))
            .WithName("GenerarReporteEjecutivo")
            .WithSummary("Genera el reporte ejecutivo de la mesa de ayuda")
            .WithDescription(
                "Los indicadores se calculan siempre de la misma forma y se devuelven junto al texto, " +
                "de modo que cada afirmación del reporte pueda contrastarse contra el dato que la originó.")
            .Produces<ReporteEjecutivo>();

        return rutas;
    }
}
