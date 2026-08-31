using System.Globalization;
using System.Text;

namespace MesaAyuda.Api.Nucleo.Proveedores;

/// <summary>
/// Genera vectores de forma local y determinista, sin llamadas de red.
///
/// Construye una bolsa de palabras proyectada sobre un número fijo de
/// dimensiones: cada término del texto se asigna a unas pocas posiciones del
/// vector mediante una función de dispersión estable, y se pondera por su
/// frecuencia. Textos que comparten vocabulario terminan cerca entre sí.
///
/// No captura sinónimos ni matices de significado, y por eso no reemplaza a un
/// modelo de embeddings real. Su valor es otro: permite ejecutar y probar toda
/// la arquitectura de recuperación sin credenciales, sin costo y con resultados
/// reproducibles, lo que hace posible comparar después contra un modelo real y
/// medir qué aporta de más.
/// </summary>
public sealed class ProveedorEmbeddingsSimulado : IProveedorEmbeddings
{
    /// <summary>
    /// Cada término se reparte en varias posiciones para reducir el efecto de
    /// las colisiones entre palabras distintas.
    /// </summary>
    private const int ProyeccionesPorTermino = 3;

    private static readonly char[] Separadores =
        [' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'', '/', '\\', '-', '–', '—'];

    /// <summary>
    /// Palabras demasiado frecuentes en español como para aportar significado.
    /// </summary>
    private static readonly HashSet<string> PalabrasVacias = new(StringComparer.Ordinal)
    {
        "el", "la", "los", "las", "un", "una", "unos", "unas", "de", "del", "al", "a", "ante",
        "con", "en", "para", "por", "segun", "sin", "sobre", "tras", "y", "o", "u", "e", "que",
        "se", "su", "sus", "lo", "es", "son", "ser", "este", "esta", "estos", "estas", "ese",
        "esa", "esos", "esas", "como", "mas", "pero", "si", "no", "ya", "cuando", "donde"
    };

    public string Nombre => "simulado";

    public int Dimensiones => 384;

    public Task<float[]> GenerarAsync(string texto, CancellationToken cancelacion = default) =>
        Task.FromResult(Proyectar(texto));

    public Task<IReadOnlyList<float[]>> GenerarLoteAsync(
        IReadOnlyList<string> textos,
        CancellationToken cancelacion = default)
    {
        IReadOnlyList<float[]> vectores = textos.Select(Proyectar).ToArray();
        return Task.FromResult(vectores);
    }

    private float[] Proyectar(string texto)
    {
        var vector = new float[Dimensiones];
        var terminos = Segmentar(texto);

        if (terminos.Count == 0)
        {
            return vector;
        }

        foreach (var (termino, frecuencia) in terminos)
        {
            // Los términos que aparecen muchas veces no deben dominar el vector,
            // por eso el peso crece de forma logarítmica y no lineal.
            var peso = (float)(1d + Math.Log(frecuencia));

            for (var proyeccion = 0; proyeccion < ProyeccionesPorTermino; proyeccion++)
            {
                var dispersion = Dispersar(termino, proyeccion);
                var posicion = (int)(dispersion % (uint)Dimensiones);

                // El signo alternado evita que todos los términos empujen el
                // vector hacia la misma región del espacio.
                var signo = (dispersion & 1) == 0 ? 1f : -1f;

                vector[posicion] += peso * signo;
            }
        }

        return Vectores.Normalizar(vector);
    }

    private static Dictionary<string, int> Segmentar(string texto)
    {
        var frecuencias = new Dictionary<string, int>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(texto))
        {
            return frecuencias;
        }

        var partes = texto.Split(Separadores, StringSplitOptions.RemoveEmptyEntries);

        foreach (var parte in partes)
        {
            var termino = Normalizar(parte);

            // Se descartan los términos de una o dos letras y las palabras vacías.
            if (termino.Length < 3 || PalabrasVacias.Contains(termino))
            {
                continue;
            }

            frecuencias[termino] = frecuencias.GetValueOrDefault(termino) + 1;
        }

        return frecuencias;
    }

    /// <summary>
    /// Pasa a minúsculas y quita los acentos, de modo que "artículo" y
    /// "articulo" se consideren el mismo término.
    /// </summary>
    private static string Normalizar(string palabra)
    {
        var descompuesta = palabra.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var constructor = new StringBuilder(descompuesta.Length);

        foreach (var caracter in descompuesta)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(caracter))
            {
                constructor.Append(caracter);
            }
        }

        return constructor.ToString();
    }

    /// <summary>
    /// Función de dispersión FNV-1a. Se implementa aquí, y no se usa
    /// <c>string.GetHashCode</c>, porque este necesita producir el mismo valor
    /// en toda ejecución y en toda máquina: los vectores quedan guardados en la
    /// base de datos y deben seguir siendo comparables mañana.
    /// </summary>
    private static uint Dispersar(string termino, int semilla)
    {
        const uint BaseFnv = 2166136261;
        const uint PrimoFnv = 16777619;

        var dispersion = BaseFnv ^ (uint)semilla;

        foreach (var caracter in termino)
        {
            dispersion ^= caracter;
            dispersion *= PrimoFnv;
        }

        return dispersion;
    }
}
