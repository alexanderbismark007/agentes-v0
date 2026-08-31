using MesaAyuda.Api.Nucleo.Datos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MesaAyuda.Pruebas.Integracion;

/// <summary>
/// Levanta la aplicación completa en memoria y reemplaza únicamente el
/// almacenamiento, de modo que las pruebas ejerciten el mismo enrutamiento,
/// la misma validación y el mismo manejo de errores que el servicio real,
/// sin depender de una base de datos instalada.
/// </summary>
public sealed class FabricaPruebas : WebApplicationFactory<Program>
{
    private readonly string _nombreBase = $"pruebas-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder constructor)
    {
        constructor.UseEnvironment("Development");

        constructor.UseSetting("ConnectionStrings:BaseDatos", "Host=no-usada;Database=no-usada");

        // El arranque real aplica migraciones; con un almacén en memoria no
        // corresponde hacerlo, así que se desactiva ese paso.
        constructor.UseSetting("Datos:MigrarAlIniciar", "false");
        constructor.UseSetting("Proveedores:Lenguaje", "simulado");

        constructor.ConfigureServices(servicios =>
        {
            servicios.RemoveAll<DbContextOptions<ContextoMesaAyuda>>();
            servicios.RemoveAll<ContextoMesaAyuda>();

            servicios.AddDbContext<ContextoMesaAyuda>(opciones =>
                opciones.UseInMemoryDatabase(_nombreBase));
        });
    }

    /// <summary>
    /// Crea un cliente sobre una base vacía, para que cada prueba controle
    /// exactamente los datos que existen.
    /// </summary>
    public HttpClient CrearClienteConBaseLimpia()
    {
        var cliente = CreateClient();

        using var alcance = Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<ContextoMesaAyuda>();
        contexto.Database.EnsureDeleted();

        return cliente;
    }
}
