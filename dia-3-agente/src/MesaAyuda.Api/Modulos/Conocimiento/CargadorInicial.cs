using MesaAyuda.Api.Dominio.Conocimiento;
using MesaAyuda.Api.Nucleo.Configuracion;
using Microsoft.Extensions.Options;

namespace MesaAyuda.Api.Modulos.Conocimiento;

/// <summary>
/// Indexa al arrancar los documentos que estén en la carpeta configurada.
///
/// Gracias a la huella de contenido, volver a levantar el servicio no reprocesa
/// nada: solo se indexa lo que se agregó o cambió desde la última vez.
/// </summary>
public static class CargadorInicial
{
    public static async Task CargarAsync(
        IServiceProvider servicios,
        IOptions<OpcionesConocimiento> opciones,
        ILogger registro,
        CancellationToken cancelacion = default)
    {
        var configuracion = opciones.Value;

        if (!configuracion.IndexarAlIniciar)
        {
            return;
        }

        var carpeta = Path.IsPathRooted(configuracion.CarpetaDocumentos)
            ? configuracion.CarpetaDocumentos
            : Path.Combine(AppContext.BaseDirectory, configuracion.CarpetaDocumentos);

        if (!Directory.Exists(carpeta))
        {
            registro.LogInformation("No existe la carpeta de documentos {Carpeta}; no hay nada que indexar.", carpeta);
            return;
        }

        var archivos = Directory.EnumerateFiles(carpeta)
            .Where(LectorDocumentos.EsAdmitido)
            .OrderBy(a => a, StringComparer.Ordinal)
            .ToArray();

        if (archivos.Length == 0)
        {
            registro.LogInformation("La carpeta {Carpeta} no contiene documentos admitidos.", carpeta);
            return;
        }

        using var alcance = servicios.CreateScope();
        var indexador = alcance.ServiceProvider.GetRequiredService<ServicioIndexacion>();

        foreach (var archivo in archivos)
        {
            try
            {
                await using var contenido = File.OpenRead(archivo);

                var resultado = await indexador.IndexarAsync(
                    contenido,
                    Path.GetFileName(archivo),
                    titulo: null,
                    NivelAcceso.Publico,
                    cancelacion);

                registro.LogInformation(
                    "{Archivo}: {Fragmentos} fragmentos{Detalle}",
                    Path.GetFileName(archivo),
                    resultado.Fragmentos,
                    resultado.SinCambios ? " (sin cambios)" : $" en {resultado.MilisegundosTotales} ms");
            }
            catch (Exception excepcion)
            {
                // Un documento defectuoso no debe impedir que el servicio
                // arranque ni que los demás documentos se indexen.
                registro.LogError(excepcion, "No se pudo indexar {Archivo}.", Path.GetFileName(archivo));
            }
        }
    }
}
