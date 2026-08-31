using System.Text.Json;
using FluentValidation;
using MesaAyuda.Api.Dominio.Auditoria;
using MesaAyuda.Api.Modulos.Orquestacion.Contratos;
using MesaAyuda.Api.Nucleo.Configuracion;
using MesaAyuda.Api.Nucleo.Errores;
using MesaAyuda.Api.Nucleo.Proveedores;
using Microsoft.Extensions.Options;

namespace MesaAyuda.Api.Modulos.Orquestacion;

/// <summary>
/// Endpoints por los que la automatización externa se comunica con el sistema.
/// </summary>
public static class OrquestacionEndpoints
{
    /// <summary>Cabecera en la que se espera la firma del cuerpo.</summary>
    public const string CabeceraFirma = "X-Firma-Mesa";

    public static IEndpointRouteBuilder MapearOrquestacion(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/v1/orquestacion")
            .WithTags("Orquestación");

        grupo.MapPost("/entrada/solicitudes", async (
                HttpRequest peticion,
                IValidator<SolicitudEntrante> validador,
                ServicioIngreso ingreso,
                ServicioAuditoria auditoria,
                IOptions<OpcionesOrquestacion> opciones,
                CancellationToken cancelacion) =>
            {
                var configuracion = opciones.Value;

                // El cuerpo se lee como texto porque la firma se calcula sobre
                // los bytes exactos recibidos: deserializar y volver a
                // serializar produciría un texto distinto y una firma distinta.
                peticion.EnableBuffering();
                using var lector = new StreamReader(peticion.Body, leaveOpen: true);
                var cuerpo = await lector.ReadToEndAsync(cancelacion);
                peticion.Body.Position = 0;

                if (string.IsNullOrWhiteSpace(cuerpo))
                {
                    throw new ExcepcionDominio("El cuerpo de la petición está vacío.");
                }

                if (configuracion.ExigirFirma)
                {
                    var firma = peticion.Headers[CabeceraFirma].FirstOrDefault();

                    if (!FirmaWebhook.EsValida(cuerpo, configuracion.SecretoWebhook, firma))
                    {
                        // El intento fallido se registra: son justamente los
                        // eventos que interesa poder revisar después.
                        await auditoria.RegistrarRechazoAsync(
                            "desconocido", cuerpo, "Firma ausente o inválida.", cancelacion);

                        return Results.Problem(
                            title: "Firma inválida",
                            detail: $"La petición debe incluir la cabecera {CabeceraFirma} con una firma válida.",
                            statusCode: StatusCodes.Status401Unauthorized);
                    }
                }

                SolicitudEntrante? entrante;
                try
                {
                    entrante = JsonSerializer.Deserialize<SolicitudEntrante>(cuerpo, OpcionesJson.Predeterminadas);
                }
                catch (JsonException)
                {
                    throw new ExcepcionDominio("El cuerpo de la petición no es JSON válido.");
                }

                if (entrante is null)
                {
                    throw new ExcepcionDominio("El cuerpo de la petición no contiene datos.");
                }

                var invalido = await validador.ValidarAsync(entrante, cancelacion);
                if (invalido is not null)
                {
                    await auditoria.RegistrarRechazoAsync(
                        entrante.Origen, cuerpo, "Datos inválidos.", cancelacion);

                    return invalido;
                }

                var resultado = await ingreso.ProcesarAsync(entrante, cuerpo, cancelacion);

                // Un duplicado no es un error: se responde 200 con la misma
                // información, para que el flujo externo pueda reintentar sin
                // miedo a duplicar el trámite.
                return Results.Ok(resultado);
            })
            .WithName("IngresarSolicitud")
            .WithSummary("Recibe una solicitud desde un flujo automatizado")
            .WithDescription(
                "Registra la solicitud, la clasifica y prepara un acuse de recibo respaldado por el " +
                "reglamento cuando corresponde. La operación es idempotente: reenviar el mismo cuerpo " +
                "no crea una segunda solicitud. Si la firma está exigida, la petición debe incluir la " +
                "cabecera de firma.")
            .Produces<ResultadoIngreso>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapGet("/eventos", async (
                ResultadoEvento? resultado,
                string? origen,
                int? pagina,
                int? tamanoPagina,
                ServicioAuditoria servicio,
                CancellationToken cancelacion) =>
                Results.Ok(await servicio.ListarAsync(
                    new FiltroEventos
                    {
                        Resultado = resultado,
                        Origen = origen,
                        Pagina = pagina ?? 1,
                        TamanoPagina = tamanoPagina ?? 20
                    },
                    cancelacion)))
            .WithName("ListarEventos")
            .WithSummary("Consulta la bitácora de eventos de automatización")
            .Produces<Modulos.Solicitudes.Contratos.PaginaRespuesta<EventoRespuesta>>();

        grupo.MapGet("/salud", async (
                ServicioAuditoria servicio,
                IOptions<OpcionesOrquestacion> opciones,
                CancellationToken cancelacion) =>
                Results.Ok(await servicio.SaludAsync(opciones.Value.ExigirFirma, cancelacion)))
            .WithName("SaludOrquestacion")
            .WithSummary("Indicadores de salud de la automatización")
            .WithDescription("Un flujo automatizado que nadie observa deja de funcionar sin que nadie lo note.")
            .Produces<SaludOrquestacion>();

        grupo.MapPost("/firmar", (
                CuerpoAFirmar peticion,
                IOptions<OpcionesOrquestacion> opciones,
                IWebHostEnvironment entorno) =>
            {
                // Utilidad de apoyo para la clase: permite obtener la firma de
                // un cuerpo sin escribir código. Queda deshabilitada fuera de
                // desarrollo porque expone el uso del secreto compartido.
                if (entorno.IsProduction())
                {
                    return Results.NotFound();
                }

                var firma = FirmaWebhook.Calcular(peticion.Cuerpo, opciones.Value.SecretoWebhook);

                return Results.Ok(new
                {
                    cabecera = CabeceraFirma,
                    firma,
                    ejemplo = $"{CabeceraFirma}: sha256={firma}"
                });
            })
            .WithName("FirmarCuerpo")
            .WithSummary("Calcula la firma de un cuerpo (solo fuera de producción)")
            .WithDescription("Herramienta de apoyo para configurar el flujo externo durante la sesión.");

        return rutas;
    }

    /// <summary>
    /// Cuerpo que se desea firmar, para la utilidad de apoyo.
    /// </summary>
    public sealed record CuerpoAFirmar
    {
        public string Cuerpo { get; init; } = string.Empty;
    }
}
