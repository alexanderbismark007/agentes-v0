using System.Diagnostics;
using System.Text;
using MesaAyuda.Api.Dominio.Auditoria;
using MesaAyuda.Api.Dominio.Conocimiento;
using MesaAyuda.Api.Modulos.Conocimiento;
using MesaAyuda.Api.Modulos.Orquestacion.Contratos;
using MesaAyuda.Api.Modulos.Solicitudes;
using MesaAyuda.Api.Modulos.Solicitudes.Contratos;
using MesaAyuda.Api.Nucleo.Datos;
using Microsoft.EntityFrameworkCore;

namespace MesaAyuda.Api.Modulos.Orquestacion;

/// <summary>
/// Procesa una solicitud que llega desde un flujo automatizado.
///
/// Reúne en un solo paso lo construido en los tres días anteriores: registra la
/// solicitud con su clasificación (día 1), consulta el reglamento para preparar
/// una respuesta con respaldo (día 2) y deja todo asentado en la bitácora para
/// que el proceso sea auditable.
///
/// Dos propiedades hacen que esto sea utilizable en producción y no solo en una
/// demostración:
///
/// - Es idempotente: reenviar el mismo evento no crea una segunda solicitud.
///   Toda automatización reintenta, y sin esto un reintento duplicaría trámites.
/// - Es auditable: cada evento queda registrado con lo que llegó, lo que se
///   decidió y cuánto tardó, se haya procesado bien o mal.
/// </summary>
public sealed class ServicioIngreso(
    ContextoMesaAyuda contexto,
    ServicioSolicitudes solicitudes,
    ServicioConsultas consultas,
    ILogger<ServicioIngreso> registro)
{
    private const string TipoEvento = "solicitud.recibida";

    public async Task<ResultadoIngreso> ProcesarAsync(
        SolicitudEntrante entrante,
        string cuerpoOriginal,
        CancellationToken cancelacion = default)
    {
        var cronometro = Stopwatch.StartNew();
        var huella = FirmaWebhook.Huella(cuerpoOriginal);

        // Idempotencia: si ya se proceso un evento con el mismo contenido, se
        // devuelve el resultado anterior en lugar de duplicar la solicitud.
        var previo = await contexto.Eventos
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.Huella == huella && e.Resultado == ResultadoEvento.Procesado,
                cancelacion);

        if (previo is not null)
        {
            registro.LogInformation(
                "Evento duplicado descartado. Referencia previa: {Referencia}", previo.Referencia);

            return await ReconstruirDesdeAsync(previo, cronometro, cancelacion);
        }

        var evento = new EventoAuditoria(TipoEvento, entrante.Origen, huella, Recortar(cuerpoOriginal));
        contexto.Eventos.Add(evento);
        await contexto.SaveChangesAsync(cancelacion);

        try
        {
            var creada = await solicitudes.CrearAsync(
                new CrearSolicitud
                {
                    Titulo = entrante.Titulo,
                    Descripcion = entrante.Descripcion,
                    SolicitanteNombre = entrante.SolicitanteNombre,
                    SolicitanteCorreo = entrante.SolicitanteCorreo
                },
                cancelacion);

            if (!string.IsNullOrWhiteSpace(entrante.ReferenciaExterna))
            {
                await solicitudes.AgregarComentarioAsync(
                    creada.Id,
                    new CrearComentario
                    {
                        Autor = entrante.Origen,
                        Contenido = $"Ingreso automatizado. Referencia externa: {entrante.ReferenciaExterna}.",
                        EsInterno = true
                    },
                    cancelacion);
            }

            var respuesta = await PrepararRespuestaAsync(entrante, creada, cancelacion);

            evento.RegistrarExito(creada.Codigo, (int)cronometro.ElapsedMilliseconds);
            await contexto.SaveChangesAsync(cancelacion);

            registro.LogInformation(
                "Solicitud {Codigo} ingresada desde {Origen} en {Milisegundos} ms",
                creada.Codigo, entrante.Origen, cronometro.ElapsedMilliseconds);

            return new ResultadoIngreso
            {
                EventoId = evento.Id,
                Codigo = creada.Codigo,
                SolicitudId = creada.Id,
                Categoria = creada.Categoria,
                Prioridad = creada.Prioridad,
                UnidadDestino = creada.UnidadDestino,
                CorreoDestinatario = creada.SolicitanteCorreo,
                AsuntoRespuesta = $"[{creada.Codigo}] Recibimos su solicitud",
                CuerpoRespuesta = respuesta.Cuerpo,
                RespuestaConRespaldo = respuesta.ConRespaldo,
                Duplicado = false,
                MilisegundosTotales = (int)cronometro.ElapsedMilliseconds
            };
        }
        catch (Exception excepcion)
        {
            // El fallo se registra antes de propagarse: un evento que falla sin
            // dejar rastro es un trámite perdido sin forma de reconstruirlo.
            evento.RegistrarFallo(excepcion.Message, (int)cronometro.ElapsedMilliseconds);
            await contexto.SaveChangesAsync(CancellationToken.None);

            registro.LogError(excepcion, "Falló el ingreso automatizado desde {Origen}.", entrante.Origen);
            throw;
        }
    }

    /// <summary>
    /// Arma el acuse de recibo. Si el reglamento contiene información sobre lo
    /// consultado, la incluye con sus fuentes; si no, se limita a confirmar la
    /// recepción sin afirmar nada sobre el fondo del asunto.
    /// </summary>
    private async Task<(string Cuerpo, bool ConRespaldo)> PrepararRespuestaAsync(
        SolicitudEntrante entrante,
        SolicitudRespuesta creada,
        CancellationToken cancelacion)
    {
        var constructor = new StringBuilder();

        constructor.Append("Estimado(a) ").Append(creada.SolicitanteNombre).AppendLine(":").AppendLine();
        constructor
            .Append("Su solicitud fue registrada con el código ").Append(creada.Codigo)
            .Append(" y derivada a ").Append(creada.UnidadDestino).AppendLine(".").AppendLine();

        // La consulta se hace con nivel público: la respuesta va al solicitante.
        var consulta = await consultas.ResponderAsync(
            $"{entrante.Titulo}. {entrante.Descripcion}",
            NivelAcceso.Publico,
            cancelacion);

        if (!consulta.TieneRespaldo)
        {
            constructor.AppendLine(
                "Su caso será revisado por la unidad responsable, que se comunicará con usted.");

            return (Cerrar(constructor), false);
        }

        constructor.AppendLine("Según la normativa vigente:").AppendLine();
        constructor.AppendLine(consulta.Respuesta).AppendLine();
        constructor.AppendLine("Fuentes consultadas:");

        foreach (var fuente in consulta.Fuentes)
        {
            constructor.Append("  [").Append(fuente.Numero).Append("] ").Append(fuente.Documento);

            if (!string.IsNullOrWhiteSpace(fuente.Referencia))
            {
                constructor.Append(", ").Append(fuente.Referencia);
            }

            constructor.AppendLine();
        }

        constructor.AppendLine();
        constructor.AppendLine(
            "Esta respuesta es orientativa y fue generada automáticamente. " +
            "La unidad responsable confirmará el trámite.");

        return (Cerrar(constructor), true);
    }

    private static string Cerrar(StringBuilder constructor)
    {
        constructor.AppendLine();
        constructor.AppendLine("Mesa de Ayuda Institucional");

        return constructor.ToString();
    }

    /// <summary>
    /// Rearma la respuesta de un evento ya procesado, para que un reintento
    /// reciba exactamente lo mismo que recibió el envío original.
    /// </summary>
    private async Task<ResultadoIngreso> ReconstruirDesdeAsync(
        EventoAuditoria previo,
        Stopwatch cronometro,
        CancellationToken cancelacion)
    {
        var pagina = await solicitudes.ListarAsync(
            new FiltroSolicitudes { Texto = previo.Referencia, TamanoPagina = 5 },
            cancelacion);

        var solicitud = pagina.Elementos.FirstOrDefault(s =>
            string.Equals(s.Codigo, previo.Referencia, StringComparison.OrdinalIgnoreCase));

        return new ResultadoIngreso
        {
            EventoId = previo.Id,
            Codigo = previo.Referencia ?? string.Empty,
            SolicitudId = solicitud?.Id ?? Guid.Empty,
            Categoria = solicitud?.Categoria ?? "Otra",
            Prioridad = solicitud?.Prioridad ?? "Media",
            UnidadDestino = solicitud?.UnidadDestino ?? "Mesa de Ayuda",
            CorreoDestinatario = solicitud?.SolicitanteCorreo ?? string.Empty,
            AsuntoRespuesta = $"[{previo.Referencia}] Recibimos su solicitud",
            CuerpoRespuesta =
                $"La solicitud {previo.Referencia} ya había sido registrada previamente. " +
                "No se generó un nuevo trámite.",
            RespuestaConRespaldo = false,
            Duplicado = true,
            MilisegundosTotales = (int)cronometro.ElapsedMilliseconds
        };
    }

    private static string Recortar(string cuerpo) =>
        cuerpo.Length <= 4000 ? cuerpo : cuerpo[..4000];
}
