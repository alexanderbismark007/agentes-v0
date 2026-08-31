namespace MesaAyuda.Api.Nucleo.Proveedores;

/// <summary>
/// Convierte texto en vectores numéricos que representan su significado.
///
/// Dos textos que hablan de lo mismo producen vectores cercanos entre sí,
/// aunque no compartan las mismas palabras. Esa propiedad es la que permite
/// buscar por sentido y no por coincidencia literal.
/// </summary>
public interface IProveedorEmbeddings
{
    /// <summary>Nombre corto del proveedor, usado en registros y auditoría.</summary>
    string Nombre { get; }

    /// <summary>
    /// Cantidad de dimensiones de los vectores que produce. La base de datos
    /// reserva el espacio en función de este valor, de modo que no puede
    /// cambiarse sin migrar los vectores ya almacenados.
    /// </summary>
    int Dimensiones { get; }

    /// <summary>
    /// Genera el vector de un texto.
    /// </summary>
    Task<float[]> GenerarAsync(string texto, CancellationToken cancelacion = default);

    /// <summary>
    /// Genera los vectores de varios textos en una sola operación. Al indexar
    /// un documento extenso esto evita cientos de llamadas independientes.
    /// </summary>
    Task<IReadOnlyList<float[]>> GenerarLoteAsync(
        IReadOnlyList<string> textos,
        CancellationToken cancelacion = default);
}

/// <summary>
/// Operaciones sobre vectores que se usan tanto al indexar como al comparar.
/// </summary>
public static class Vectores
{
    /// <summary>
    /// Similitud del coseno entre dos vectores: 1 significa misma dirección,
    /// 0 significa sin relación y -1 significa dirección opuesta.
    /// </summary>
    public static double SimilitudCoseno(IReadOnlyList<float> primero, IReadOnlyList<float> segundo)
    {
        if (primero.Count != segundo.Count)
        {
            throw new ArgumentException(
                $"No se pueden comparar vectores de distinta dimensión ({primero.Count} y {segundo.Count}).");
        }

        double producto = 0d, normaPrimero = 0d, normaSegundo = 0d;

        for (var i = 0; i < primero.Count; i++)
        {
            producto += primero[i] * segundo[i];
            normaPrimero += primero[i] * primero[i];
            normaSegundo += segundo[i] * segundo[i];
        }

        if (normaPrimero == 0d || normaSegundo == 0d)
        {
            return 0d;
        }

        return producto / (Math.Sqrt(normaPrimero) * Math.Sqrt(normaSegundo));
    }

    /// <summary>
    /// Lleva el vector a longitud uno. Trabajar con vectores normalizados hace
    /// que la comparación dependa solo de la dirección y no de la magnitud.
    /// </summary>
    public static float[] Normalizar(float[] vector)
    {
        var norma = Math.Sqrt(vector.Sum(v => (double)v * v));

        if (norma == 0d)
        {
            return vector;
        }

        var resultado = new float[vector.Length];
        for (var i = 0; i < vector.Length; i++)
        {
            resultado[i] = (float)(vector[i] / norma);
        }

        return resultado;
    }
}
