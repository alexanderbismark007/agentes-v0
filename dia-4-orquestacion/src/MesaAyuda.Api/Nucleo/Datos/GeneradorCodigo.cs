using MesaAyuda.Api.Dominio.Solicitudes;
using Microsoft.EntityFrameworkCore;

namespace MesaAyuda.Api.Nucleo.Datos;

/// <summary>
/// Produce el código legible de cada solicitud con el formato SOL-{año}-{correlativo}.
/// </summary>
public interface IGeneradorCodigo
{
    Task<string> SiguienteAsync(CancellationToken cancelacion = default);
}

/// <inheritdoc />
public sealed class GeneradorCodigo(ContextoMesaAyuda contexto) : IGeneradorCodigo
{
    public async Task<string> SiguienteAsync(CancellationToken cancelacion = default)
    {
        var anio = DateTimeOffset.UtcNow.Year;
        var prefijo = $"SOL-{anio}-";

        var emitidos = await contexto.Solicitudes
            .Where(s => s.Codigo.StartsWith(prefijo))
            .CountAsync(cancelacion);

        return $"{prefijo}{emitidos + 1:D6}";
    }
}
