using MesaAyuda.Api.Nucleo.Configuracion;
using Microsoft.Extensions.Options;

namespace MesaAyuda.Api.Nucleo.Proveedores;

/// <summary>
/// Registro de los proveedores de modelos. El proveedor activo se resuelve una
/// sola vez, aquí, a partir de la configuración: el resto de la aplicación
/// únicamente pide <see cref="IProveedorLenguaje"/> y no sabe cuál está en uso.
/// </summary>
public static class ExtensionesProveedores
{
    public static IServiceCollection AgregarProveedores(
        this IServiceCollection servicios,
        IConfiguration configuracion)
    {
        servicios.Configure<OpcionesProveedores>(configuracion.GetSection(OpcionesProveedores.Seccion));

        var opciones = configuracion.GetSection(OpcionesProveedores.Seccion).Get<OpcionesProveedores>()
                       ?? new OpcionesProveedores();

        servicios.AddSingleton<ProveedorSimulado>();

        servicios.AddHttpClient<ProveedorNube>((proveedorServicios, cliente) =>
        {
            var config = proveedorServicios.GetRequiredService<IOptions<OpcionesProveedores>>().Value.Nube;
            cliente.BaseAddress = new Uri(config.UrlBase.TrimEnd('/') + "/");
            cliente.Timeout = TimeSpan.FromSeconds(config.TiempoEsperaSegundos);
        });

        servicios.AddScoped<IProveedorLenguaje>(proveedorServicios =>
        {
            var nombre = opciones.Lenguaje?.Trim().ToLowerInvariant();

            return nombre switch
            {
                "nube" => proveedorServicios.GetRequiredService<ProveedorNube>(),
                "simulado" or null or "" => proveedorServicios.GetRequiredService<ProveedorSimulado>(),
                _ => throw new InvalidOperationException(
                    $"Proveedor de lenguaje '{opciones.Lenguaje}' no reconocido. Valores admitidos: simulado, nube.")
            };
        });

        return servicios;
    }
}
