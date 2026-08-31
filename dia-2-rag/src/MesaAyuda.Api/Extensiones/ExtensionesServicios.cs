using FluentValidation;
using MesaAyuda.Api.Modulos.Conocimiento;
using MesaAyuda.Api.Modulos.Solicitudes;
using MesaAyuda.Api.Nucleo.Configuracion;
using MesaAyuda.Api.Nucleo.Datos;
using MesaAyuda.Api.Nucleo.Errores;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

namespace MesaAyuda.Api.Extensiones;

/// <summary>
/// Agrupa el registro de dependencias por área, para que el archivo de arranque
/// se lea como un índice y no como una lista interminable de llamadas.
/// </summary>
public static class ExtensionesServicios
{
    public static IServiceCollection AgregarPersistencia(
        this IServiceCollection servicios,
        IConfiguration configuracion)
    {
        var cadena = configuracion.GetConnectionString("BaseDatos")
                     ?? throw new InvalidOperationException(
                         "Falta la cadena de conexión 'BaseDatos'. Revise appsettings.json o la variable " +
                         "ConnectionStrings__BaseDatos.");

        servicios.AddDbContext<ContextoMesaAyuda>(opciones =>
            opciones.UseNpgsql(cadena, npgsql =>
            {
                npgsql.MigrationsHistoryTable("historial_migraciones", "mesa");

                // Habilita el mapeo del tipo vectorial usado por el módulo de
                // conocimiento para la búsqueda por similitud.
                npgsql.UseVector();
            }));

        servicios.AddScoped<IGeneradorCodigo, GeneradorCodigo>();

        return servicios;
    }

    public static IServiceCollection AgregarModulos(
        this IServiceCollection servicios,
        IConfiguration configuracion)
    {
        // Módulo de solicitudes
        servicios.AddScoped<IClasificadorSolicitudes, ClasificadorSolicitudes>();
        servicios.AddScoped<ServicioSolicitudes>();
        servicios.AddScoped<ServicioEstadisticas>();

        // Módulo de conocimiento
        servicios.Configure<OpcionesConocimiento>(configuracion.GetSection(OpcionesConocimiento.Seccion));
        servicios.AddSingleton<LectorDocumentos>();
        servicios.AddSingleton<Fragmentador>();
        servicios.AddScoped<ServicioIndexacion>();
        servicios.AddScoped<ServicioConsultas>();
        servicios.AddScoped<IBuscadorSemantico, BuscadorSemanticoPostgres>();

        servicios.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

        return servicios;
    }

    public static IServiceCollection AgregarDocumentacion(this IServiceCollection servicios)
    {
        servicios.AddEndpointsApiExplorer();

        servicios.AddSwaggerGen(opciones =>
        {
            opciones.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Mesa de Ayuda Institucional",
                Version = "v1",
                Description =
                    "API de registro y seguimiento de solicitudes institucionales, con consulta " +
                    "documental sobre el reglamento. Cada respuesta documental devuelve las fuentes " +
                    "que la respaldan y su nivel de similitud, de modo que toda afirmación pueda verificarse."
            });

            var archivoXml = Path.Combine(AppContext.BaseDirectory, "MesaAyuda.Api.xml");
            if (File.Exists(archivoXml))
            {
                opciones.IncludeXmlComments(archivoXml);
            }
        });

        return servicios;
    }

    public static IServiceCollection AgregarManejoDeErrores(this IServiceCollection servicios)
    {
        servicios.AddExceptionHandler<ManejadorProblemas>();
        servicios.AddProblemDetails();

        return servicios;
    }
}
