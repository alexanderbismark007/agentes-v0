namespace MesaAyuda.Api.Nucleo.Configuracion;

/// <summary>
/// Tamaño de los vectores que maneja el sistema.
/// </summary>
public static class Dimensiones
{
    /// <summary>
    /// Dimensión única para todos los proveedores de embeddings.
    ///
    /// La columna vectorial de la base de datos se declara con este tamaño, de
    /// modo que cambiar de proveedor obliga a reindexar los documentos pero no
    /// a migrar el esquema. Se eligió 384 porque es una dimensión que los tres
    /// proveedores pueden producir: el simulado la genera por construcción, el
    /// servicio en la nube admite recortar su salida a un tamaño solicitado y
    /// los modelos locales compactos la usan de forma nativa.
    /// </summary>
    public const int Estandar = 384;
}
