using System.Security.Cryptography;
using System.Text;
using MesaAyuda.Api.Nucleo.Errores;
using UglyToad.PdfPig;

namespace MesaAyuda.Api.Modulos.Conocimiento;

/// <summary>
/// Contenido textual extraído de un archivo.
/// </summary>
/// <param name="Texto">Texto plano del documento.</param>
/// <param name="Huella">Resumen del contenido, para detectar cambios.</param>
public sealed record ContenidoDocumento(string Texto, string Huella);

/// <summary>
/// Extrae el texto de los archivos que alimentan la base de conocimiento.
///
/// La extracción se aísla detrás de esta clase porque cada formato tiene sus
/// particularidades y ninguna de ellas debería contaminar la lógica de
/// indexación ni la de consulta.
/// </summary>
public sealed class LectorDocumentos
{
    private static readonly string[] ExtensionesAdmitidas = [".pdf", ".txt", ".md"];

    public static bool EsAdmitido(string nombreArchivo) =>
        ExtensionesAdmitidas.Contains(Path.GetExtension(nombreArchivo), StringComparer.OrdinalIgnoreCase);

    public static string ExtensionesSoportadas => string.Join(", ", ExtensionesAdmitidas);

    /// <summary>
    /// Lee el contenido de un archivo según su extensión.
    /// </summary>
    public ContenidoDocumento Leer(Stream contenido, string nombreArchivo)
    {
        using var memoria = new MemoryStream();
        contenido.CopyTo(memoria);
        var bytes = memoria.ToArray();

        if (bytes.Length == 0)
        {
            throw new ExcepcionDominio($"El archivo '{nombreArchivo}' está vacío.");
        }

        var extension = Path.GetExtension(nombreArchivo).ToLowerInvariant();

        var texto = extension switch
        {
            ".pdf" => ExtraerDePdf(bytes, nombreArchivo),
            ".txt" or ".md" => Encoding.UTF8.GetString(bytes),
            _ => throw new ExcepcionDominio(
                $"El formato '{extension}' no está soportado. Formatos admitidos: {ExtensionesSoportadas}.")
        };

        if (string.IsNullOrWhiteSpace(texto))
        {
            throw new ExcepcionDominio(
                $"No se pudo extraer texto de '{nombreArchivo}'. " +
                "Si es un PDF escaneado, necesita reconocimiento óptico de caracteres antes de indexarse.");
        }

        return new ContenidoDocumento(texto, CalcularHuella(bytes));
    }

    private static string ExtraerDePdf(byte[] bytes, string nombreArchivo)
    {
        try
        {
            using var documento = PdfDocument.Open(bytes);
            var constructor = new StringBuilder();

            foreach (var pagina in documento.GetPages())
            {
                var textoPagina = pagina.Text;

                if (string.IsNullOrWhiteSpace(textoPagina))
                {
                    continue;
                }

                // Se marca el número de página para poder citarlo después.
                constructor.Append("Página ").Append(pagina.Number).Append("\n\n");
                constructor.Append(textoPagina).Append("\n\n");
            }

            return constructor.ToString();
        }
        catch (Exception excepcion) when (excepcion is not ExcepcionDominio)
        {
            throw new ExcepcionDominio($"No se pudo leer el PDF '{nombreArchivo}': {excepcion.Message}");
        }
    }

    /// <summary>
    /// Calcula la huella del contenido para saber si un documento ya indexado
    /// cambió y evitar reprocesarlo sin necesidad.
    /// </summary>
    public static string CalcularHuella(byte[] contenido) =>
        Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();
}
