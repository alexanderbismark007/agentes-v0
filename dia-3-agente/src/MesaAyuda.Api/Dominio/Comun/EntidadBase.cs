namespace MesaAyuda.Api.Dominio.Comun;

/// <summary>
/// Base de todas las entidades persistidas. Concentra la identidad y las marcas
/// de tiempo para que ninguna entidad tenga que repetir esa responsabilidad.
/// </summary>
public abstract class EntidadBase
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTimeOffset FechaCreacion { get; protected set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset FechaActualizacion { get; protected set; } = DateTimeOffset.UtcNow;

    protected void MarcarActualizacion() => FechaActualizacion = DateTimeOffset.UtcNow;

    /// <summary>
    /// Reubica la fecha de creación de la entidad. Existe únicamente para poder
    /// generar datos de ejemplo con antigüedad realista y no debe usarse dentro
    /// del flujo normal de la aplicación.
    /// </summary>
    internal void FijarFechaCreacion(DateTimeOffset fecha)
    {
        FechaCreacion = fecha;

        if (FechaActualizacion < fecha)
        {
            FechaActualizacion = fecha;
        }
    }
}
