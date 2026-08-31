using MesaAyuda.Api.Extensiones;
using MesaAyuda.Api.Modulos.Solicitudes;
using MesaAyuda.Api.Nucleo.Datos;
using MesaAyuda.Api.Nucleo.Proveedores;
using Microsoft.EntityFrameworkCore;

var constructor = WebApplication.CreateBuilder(args);

constructor.Services
    .AgregarPersistencia(constructor.Configuration)
    .AgregarProveedores(constructor.Configuration)
    .AgregarModulos()
    .AgregarDocumentacion()
    .AgregarManejoDeErrores();

var aplicacion = constructor.Build();

aplicacion.UseExceptionHandler();

// La documentación interactiva queda disponible solo fuera de producción.
if (!aplicacion.Environment.IsProduction())
{
    aplicacion.UseSwagger();
    aplicacion.UseSwaggerUI(opciones =>
    {
        opciones.SwaggerEndpoint("/swagger/v1/swagger.json", "Mesa de Ayuda v1");
        opciones.DocumentTitle = "Mesa de Ayuda Institucional";
    });
}

aplicacion.MapearSolicitudes();

aplicacion.MapGet("/salud", (IProveedorLenguaje proveedor) => Results.Ok(new
    {
        estado = "activo",
        proveedorLenguaje = proveedor.Nombre,
        momento = DateTimeOffset.UtcNow
    }))
    .WithTags("Operación")
    .WithName("Salud")
    .WithSummary("Verifica que el servicio responde e informa el proveedor activo");

aplicacion.MapGet("/", () => Results.Redirect("/swagger"))
    .ExcludeFromDescription();

await PrepararBaseDeDatosAsync(aplicacion);

await aplicacion.RunAsync();

/// <summary>
/// Aplica las migraciones pendientes y carga los datos de ejemplo. En un
/// despliegue real las migraciones se ejecutan como un paso previo e
/// independiente; aquí se hacen al arrancar para que el proyecto quede
/// utilizable con un solo comando.
/// </summary>
static async Task PrepararBaseDeDatosAsync(WebApplication aplicacion)
{
    if (aplicacion.Configuration.GetValue("Datos:MigrarAlIniciar", true) is false)
    {
        return;
    }

    using var alcance = aplicacion.Services.CreateScope();
    var contexto = alcance.ServiceProvider.GetRequiredService<ContextoMesaAyuda>();
    var registro = alcance.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await contexto.Database.MigrateAsync();

        if (aplicacion.Configuration.GetValue("Datos:SembrarAlIniciar", true))
        {
            await SembradorDatos.SembrarAsync(contexto);
        }

        registro.LogInformation("Base de datos preparada correctamente.");
    }
    catch (Exception excepcion)
    {
        registro.LogError(excepcion, "No se pudo preparar la base de datos.");
        throw;
    }
}

/// <summary>
/// Se expone la clase de arranque para que el proyecto de pruebas pueda
/// levantar la aplicación en memoria.
/// </summary>
public partial class Program;
