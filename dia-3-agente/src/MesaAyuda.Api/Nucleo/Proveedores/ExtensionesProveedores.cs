using MesaAyuda.Api.Nucleo.Configuracion;
using Microsoft.Extensions.Options;

namespace MesaAyuda.Api.Nucleo.Proveedores;

/// <summary>
/// Registro de los proveedores de modelos. Los proveedores activos se resuelven
/// una sola vez, aquí, a partir de la configuración: el resto de la aplicación
/// únicamente pide las interfaces y no sabe cuál implementación está en uso.
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
        servicios.AddSingleton<ProveedorEmbeddingsSimulado>();

        servicios.AddHttpClient<ProveedorNube>(ConfigurarClienteNube);
        servicios.AddHttpClient<ProveedorEmbeddingsNube>(ConfigurarClienteNube);

        servicios.AddScoped<IProveedorLenguaje>(proveedorServicios =>
            Resolver<IProveedorLenguaje>(
                opciones.Lenguaje,
                "lenguaje",
                () => proveedorServicios.GetRequiredService<ProveedorSimulado>(),
                () => proveedorServicios.GetRequiredService<ProveedorNube>()));

        servicios.AddScoped<IProveedorEmbeddings>(proveedorServicios =>
            Resolver<IProveedorEmbeddings>(
                opciones.Embeddings,
                "embeddings",
                () => proveedorServicios.GetRequiredService<ProveedorEmbeddingsSimulado>(),
                () => proveedorServicios.GetRequiredService<ProveedorEmbeddingsNube>()));

        return servicios;
    }

    private static void ConfigurarClienteNube(IServiceProvider proveedorServicios, HttpClient cliente)
    {
        var config = proveedorServicios.GetRequiredService<IOptions<OpcionesProveedores>>().Value.Nube;

        cliente.BaseAddress = new Uri(config.UrlBase.TrimEnd('/') + "/");
        cliente.Timeout = TimeSpan.FromSeconds(config.TiempoEsperaSegundos);
    }

    private static T Resolver<T>(
        string? configurado,
        string tipo,
        Func<T> simulado,
        Func<T> nube)
    {
        var nombre = configurado?.Trim().ToLowerInvariant();

        return nombre switch
        {
            "nube" => nube(),
            "simulado" or null or "" => simulado(),
            _ => throw new InvalidOperationException(
                $"Proveedor de {tipo} '{configurado}' no reconocido. Valores admitidos: simulado, nube.")
        };
    }
}
