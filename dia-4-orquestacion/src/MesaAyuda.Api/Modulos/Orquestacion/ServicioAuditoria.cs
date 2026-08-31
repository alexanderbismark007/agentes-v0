using MesaAyuda.Api.Dominio.Auditoria;
using MesaAyuda.Api.Modulos.Orquestacion.Contratos;
using MesaAyuda.Api.Modulos.Solicitudes.Contratos;
using MesaAyuda.Api.Nucleo.Datos;
using Microsoft.EntityFrameworkCore;

namespace MesaAyuda.Api.Modulos.Orquestacion;

/// <summary>
/// Consulta la bitácora de eventos y calcula la salud de la automatización.
///
/// Un flujo automatizado que nadie observa deja de funcionar sin que nadie se
/// entere. Estos indicadores existen para que esa falla sea visible.
/// </summary>
public sealed class ServicioAuditoria(ContextoMesaAyuda contexto)
{
    public async Task<PaginaRespuesta<EventoRespuesta>> ListarAsync(
        FiltroEventos filtro,
        CancellationToken cancelacion = default)
    {
        var pagina = Math.Max(filtro.Pagina, 1);
        var tamano = Math.Clamp(filtro.TamanoPagina, 1, 100);

        var consulta = contexto.Eventos.AsNoTracking().AsQueryable();

        if (filtro.Resultado.HasValue)
        {
            consulta = consulta.Where(e => e.Resultado == filtro.Resultado.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Origen))
        {
            var origen = filtro.Origen.Trim().ToLower();
            consulta = consulta.Where(e => e.Origen.ToLower() == origen);
        }

        var total = await consulta.CountAsync(cancelacion);

        var elementos = await consulta
            .OrderByDescending(e => e.FechaCreacion)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync(cancelacion);

        return new PaginaRespuesta<EventoRespuesta>
        {
            Elementos = elementos.Select(EventoRespuesta.Desde).ToArray(),
            Pagina = pagina,
            TamanoPagina = tamano,
            Total = total
        };
    }

    public async Task<SaludOrquestacion> SaludAsync(
        bool firmaExigida,
        CancellationToken cancelacion = default)
    {
        var eventos = await contexto.Eventos
            .AsNoTracking()
            .Select(e => new { e.Resultado, e.Milisegundos, e.FechaCreacion })
            .ToListAsync(cancelacion);

        var procesados = eventos.Count(e => e.Resultado == ResultadoEvento.Procesado);
        var fallidos = eventos.Count(e => e.Resultado == ResultadoEvento.Fallido);

        // La tasa se calcula sobre los eventos que se intentaron procesar: los
        // duplicados y los rechazados no son fallas del procesamiento.
        var intentados = procesados + fallidos;

        var tiempos = eventos
            .Where(e => e.Resultado == ResultadoEvento.Procesado && e.Milisegundos > 0)
            .Select(e => (double)e.Milisegundos)
            .ToList();

        return new SaludOrquestacion
        {
            TotalEventos = eventos.Count,
            Procesados = procesados,
            Fallidos = fallidos,
            Duplicados = eventos.Count(e => e.Resultado == ResultadoEvento.Duplicado),
            Rechazados = eventos.Count(e => e.Resultado == ResultadoEvento.Rechazado),
            TasaExito = intentados == 0 ? 0d : Math.Round(procesados * 100d / intentados, 1),
            MilisegundosPromedio = tiempos.Count == 0 ? 0d : Math.Round(tiempos.Average(), 1),
            FirmaExigida = firmaExigida,
            UltimoEvento = eventos.Count == 0 ? null : eventos.Max(e => e.FechaCreacion)
        };
    }

    /// <summary>
    /// Registra un evento que se rechazó antes de intentar procesarlo, por
    /// ejemplo por firma inválida. Estos intentos también deben quedar
    /// asentados: son justamente los que interesa revisar.
    /// </summary>
    public async Task RegistrarRechazoAsync(
        string origen,
        string cuerpo,
        string motivo,
        CancellationToken cancelacion = default)
    {
        var evento = new EventoAuditoria(
            "solicitud.rechazada",
            origen,
            FirmaWebhook.Huella(cuerpo),
            cuerpo.Length <= 4000 ? cuerpo : cuerpo[..4000]);

        evento.RegistrarRechazo(motivo);

        contexto.Eventos.Add(evento);
        await contexto.SaveChangesAsync(cancelacion);
    }
}
