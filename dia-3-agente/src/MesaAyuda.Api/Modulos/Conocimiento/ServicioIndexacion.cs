using System.Diagnostics;
using MesaAyuda.Api.Dominio.Conocimiento;
using MesaAyuda.Api.Modulos.Conocimiento.Contratos;
using MesaAyuda.Api.Nucleo.Datos;
using MesaAyuda.Api.Nucleo.Errores;
using MesaAyuda.Api.Nucleo.Proveedores;
using Microsoft.EntityFrameworkCore;

namespace MesaAyuda.Api.Modulos.Conocimiento;

/// <summary>
/// Incorpora documentos a la base de conocimiento: extrae el texto, lo divide
/// en fragmentos, genera sus vectores y los guarda.
/// </summary>
public sealed class ServicioIndexacion(
    ContextoMesaAyuda contexto,
    LectorDocumentos lector,
    Fragmentador fragmentador,
    IProveedorEmbeddings proveedorEmbeddings,
    ILogger<ServicioIndexacion> registro)
{
    public async Task<ResultadoIndexacion> IndexarAsync(
        Stream contenido,
        string nombreArchivo,
        string? titulo,
        NivelAcceso nivelAcceso,
        CancellationToken cancelacion = default)
    {
        var cronometro = Stopwatch.StartNew();

        if (!LectorDocumentos.EsAdmitido(nombreArchivo))
        {
            throw new ExcepcionDominio(
                $"El archivo '{nombreArchivo}' no tiene un formato admitido. " +
                $"Formatos válidos: {LectorDocumentos.ExtensionesSoportadas}.");
        }

        var documentoLeido = lector.Leer(contenido, nombreArchivo);
        var tituloFinal = string.IsNullOrWhiteSpace(titulo)
            ? Path.GetFileNameWithoutExtension(nombreArchivo)
            : titulo.Trim();

        var existente = await contexto.Documentos
            .Include(d => d.Fragmentos)
            .FirstOrDefaultAsync(d => d.NombreArchivo == nombreArchivo, cancelacion);

        // Si el archivo ya está indexado con el mismo contenido y el mismo
        // modelo, reprocesarlo sería gastar tiempo y dinero sin cambiar nada.
        if (existente is not null
            && existente.HuellaContenido == documentoLeido.Huella
            && existente.ModeloEmbeddings == proveedorEmbeddings.Nombre)
        {
            registro.LogInformation("El documento {Archivo} ya estaba indexado y sin cambios.", nombreArchivo);

            return new ResultadoIndexacion
            {
                DocumentoId = existente.Id,
                Titulo = existente.Titulo,
                Fragmentos = existente.CantidadFragmentos,
                SinCambios = true,
                MilisegundosTotales = (int)cronometro.ElapsedMilliseconds
            };
        }

        var trozos = fragmentador.Fragmentar(documentoLeido.Texto);

        if (trozos.Count == 0)
        {
            throw new ExcepcionDominio(
                $"El documento '{nombreArchivo}' no produjo ningún fragmento aprovechable.");
        }

        var vectores = await proveedorEmbeddings.GenerarLoteAsync(
            trozos.Select(t => t.Contenido).ToArray(),
            cancelacion);

        var documento = existente;

        if (documento is null)
        {
            documento = new Documento(tituloFinal, nombreArchivo, documentoLeido.Huella, nivelAcceso);
            contexto.Documentos.Add(documento);
        }
        else
        {
            documento.ActualizarMetadatos(tituloFinal, nivelAcceso, documentoLeido.Huella);

            // Los fragmentos anteriores corresponden a otro contenido o a otro
            // modelo: se eliminan antes de escribir los nuevos.
            contexto.Fragmentos.RemoveRange(contexto.Fragmentos.Where(f => f.DocumentoId == documento.Id));
        }

        var fragmentos = trozos
            .Select((trozo, indice) => new FragmentoDocumento(
                documento.Id,
                trozo.Orden,
                trozo.Contenido,
                vectores[indice],
                trozo.Referencia))
            .ToArray();

        documento.ReemplazarFragmentos(fragmentos, proveedorEmbeddings.Nombre);
        contexto.Fragmentos.AddRange(fragmentos);

        await contexto.SaveChangesAsync(cancelacion);

        registro.LogInformation(
            "Documento {Archivo} indexado en {Fragmentos} fragmentos con el proveedor {Proveedor}.",
            nombreArchivo, fragmentos.Length, proveedorEmbeddings.Nombre);

        return new ResultadoIndexacion
        {
            DocumentoId = documento.Id,
            Titulo = documento.Titulo,
            Fragmentos = fragmentos.Length,
            SinCambios = false,
            MilisegundosTotales = (int)cronometro.ElapsedMilliseconds
        };
    }

    public async Task<IReadOnlyCollection<DocumentoRespuesta>> ListarAsync(CancellationToken cancelacion = default)
    {
        var documentos = await contexto.Documentos
            .AsNoTracking()
            .OrderBy(d => d.Titulo)
            .ToListAsync(cancelacion);

        return documentos.Select(DocumentoRespuesta.Desde).ToArray();
    }

    public async Task EliminarAsync(Guid id, CancellationToken cancelacion = default)
    {
        var documento = await contexto.Documentos.FirstOrDefaultAsync(d => d.Id == id, cancelacion)
                        ?? throw new ExcepcionNoEncontrado("el documento", id);

        contexto.Documentos.Remove(documento);
        await contexto.SaveChangesAsync(cancelacion);

        registro.LogInformation("Documento {Titulo} eliminado de la base de conocimiento.", documento.Titulo);
    }
}
