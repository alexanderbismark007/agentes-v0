using MesaAyuda.Api.Nucleo.Configuracion;
using Microsoft.Extensions.Options;

namespace MesaAyuda.Api.Nucleo.Proveedores;

/// <summary>
/// Registro de los proveedores de modelos.
///
/// Este archivo es el único lugar del sistema que sabe qué implementación está
/// en uso. Agregar el proveedor local consistió en sumar dos clases y dos
/// líneas aquí: ni el módulo de conocimiento, ni el agente, ni la orquestación
/// necesitaron cambio alguno.
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

        servicios.AddHttpClient<ProveedorLocal>(ConfigurarClienteLocal);
        servicios.AddHttpClient<ProveedorEmbeddingsLocal>(ConfigurarClienteLocal);

        servicios.AddScoped<IProveedorLenguaje>(proveedorServicios =>
            Resolver<IProveedorLenguaje>(
                opciones.Lenguaje,
                "lenguaje",
                () => proveedorServicios.GetRequiredService<ProveedorSimulado>(),
                () => proveedorServicios.GetRequiredService<ProveedorNube>(),
                () => proveedorServicios.GetRequiredService<ProveedorLocal>()));

        servicios.AddScoped<IProveedorEmbeddings>(proveedorServicios =>
            Resolver<IProveedorEmbeddings>(
                opciones.Embeddings,
                "embeddings",
                () => proveedorServicios.GetRequiredService<ProveedorEmbeddingsSimulado>(),
                () => proveedorServicios.GetRequiredService<ProveedorEmbeddingsNube>(),
                () => proveedorServicios.GetRequiredService<ProveedorEmbeddingsLocal>()));

        return servicios;
    }

    private static void ConfigurarClienteNube(IServiceProvider proveedorServicios, HttpClient cliente)
    {
        var config = proveedorServicios.GetRequiredService<IOptions<OpcionesProveedores>>().Value.Nube;

        cliente.BaseAddress = new Uri(config.UrlBase.TrimEnd('/') + "/");
        cliente.Timeout = TimeSpan.FromSeconds(config.TiempoEsperaSegundos);
    }

    private static void ConfigurarClienteLocal(IServiceProvider proveedorServicios, HttpClient cliente)
    {
        var config = proveedorServicios.GetRequiredService<IOptions<OpcionesProveedores>>().Value.Local;

        cliente.BaseAddress = new Uri(config.UrlBase.TrimEnd('/') + "/");
        cliente.Timeout = TimeSpan.FromSeconds(config.TiempoEsperaSegundos);
    }

    private static T Resolver<T>(
        string? configurado,
        string tipo,
        Func<T> simulado,
        Func<T> nube,
        Func<T> local)
    {
        var nombre = configurado?.Trim().ToLowerInvariant();

        return nombre switch
        {
            "nube" => nube(),
            "local" => local(),
            "simulado" or null or "" => simulado(),
            _ => throw new InvalidOperationException(
                $"Proveedor de {tipo} '{configurado}' no reconocido. Valores admitidos: simulado, nube, local.")
        };
    }
}
