using MesaAyuda.Api.Extensiones;
using MesaAyuda.Api.Modulos.Conocimiento;
using MesaAyuda.Api.Modulos.Solicitudes;
using MesaAyuda.Api.Nucleo.Configuracion;
using MesaAyuda.Api.Nucleo.Datos;
using MesaAyuda.Api.Nucleo.Proveedores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var constructor = WebApplication.CreateBuilder(args);

constructor.Services
    .AgregarPersistencia(constructor.Configuration)
    .AgregarProveedores(constructor.Configuration)
    .AgregarModulos(constructor.Configuration)
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
aplicacion.MapearConocimiento();

aplicacion.MapGet("/salud", (
        IProveedorLenguaje proveedorLenguaje,
        IProveedorEmbeddings proveedorEmbeddings) => Results.Ok(new
    {
        estado = "activo",
        proveedorLenguaje = proveedorLenguaje.Nombre,
        proveedorEmbeddings = proveedorEmbeddings.Nombre,
        dimensionesVector = proveedorEmbeddings.Dimensiones,
        momento = DateTimeOffset.UtcNow
    }))
    .WithTags("Operación")
    .WithName("Salud")
    .WithSummary("Verifica que el servicio responde e informa los proveedores activos");

// La interfaz web se sirve desde wwwroot, generada por el proyecto de cliente.
// Cualquier ruta que no corresponda a la API ni a la documentacion devuelve la
// pagina principal, para que la navegacion del navegador funcione.
aplicacion.UseDefaultFiles();
aplicacion.UseStaticFiles();
aplicacion.MapFallbackToFile("index.html");

await PrepararAsync(aplicacion);

await aplicacion.RunAsync();

/// <summary>
/// Aplica las migraciones pendientes, carga los datos de ejemplo e indexa los
/// documentos de la carpeta configurada. En un despliegue real las migraciones
/// se ejecutan como un paso previo e independiente; aquí se hacen al arrancar
/// para que el proyecto quede utilizable con un solo comando.
/// </summary>
static async Task PrepararAsync(WebApplication aplicacion)
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

        await CargadorInicial.CargarAsync(
            aplicacion.Services,
            alcance.ServiceProvider.GetRequiredService<IOptions<OpcionesConocimiento>>(),
            registro);
    }
    catch (Exception excepcion)
    {
        registro.LogError(excepcion, "No se pudo preparar el servicio.");
        throw;
    }
}

/// <summary>
/// Se expone la clase de arranque para que el proyecto de pruebas pueda
/// levantar la aplicación en memoria.
/// </summary>
public partial class Program;
