namespace MesaAyuda.Api.Nucleo.Proveedores;

/// <summary>
/// Rol de quien emite un mensaje dentro de una conversación con el modelo.
/// </summary>
public enum RolMensaje
{
    Sistema,
    Usuario,
    Asistente
}

/// <summary>
/// Mensaje individual de una conversación.
/// </summary>
/// <param name="Rol">Quién emite el mensaje.</param>
/// <param name="Contenido">Texto del mensaje.</param>
public readonly record struct MensajeLenguaje(RolMensaje Rol, string Contenido)
{
    public static MensajeLenguaje Sistema(string contenido) => new(RolMensaje.Sistema, contenido);

    public static MensajeLenguaje Usuario(string contenido) => new(RolMensaje.Usuario, contenido);

    public static MensajeLenguaje Asistente(string contenido) => new(RolMensaje.Asistente, contenido);
}

/// <summary>
/// Petición enviada a un proveedor de lenguaje. Es deliberadamente pequeña:
/// todo lo que un proveedor concreto necesite de más se resuelve en su
/// configuración, no en este contrato.
/// </summary>
public sealed record PeticionLenguaje
{
    public required IReadOnlyList<MensajeLenguaje> Mensajes { get; init; }

    /// <summary>Grado de aleatoriedad. Cero produce la salida más estable.</summary>
    public double Temperatura { get; init; }

    /// <summary>Tope de tokens de la respuesta, para acotar costo y latencia.</summary>
    public int MaximoTokens { get; init; } = 800;

    /// <summary>Cuando es verdadero, se solicita al modelo una respuesta en JSON.</summary>
    public bool RespuestaJson { get; init; }
}

/// <summary>
/// Respuesta de un proveedor de lenguaje, incluyendo los datos de trazabilidad
/// que la mesa de ayuda necesita para auditar cada sugerencia.
/// </summary>
/// <param name="Contenido">Texto devuelto por el modelo.</param>
/// <param name="Modelo">Identificador del modelo que respondió.</param>
/// <param name="TokensEntrada">Tokens consumidos por la petición.</param>
/// <param name="TokensSalida">Tokens generados en la respuesta.</param>
public sealed record RespuestaLenguaje(
    string Contenido,
    string Modelo,
    int TokensEntrada = 0,
    int TokensSalida = 0);

/// <summary>
/// Punto único por el que la aplicación consulta un modelo de lenguaje.
/// Ningún módulo del sistema debe hablar directamente con un servicio externo:
/// todos dependen de esta interfaz, lo que permite cambiar de proveedor sin
/// tocar la lógica de negocio.
/// </summary>
public interface IProveedorLenguaje
{
    /// <summary>Nombre corto del proveedor, usado en registros y auditoría.</summary>
    string Nombre { get; }

    /// <summary>Envía una conversación al modelo y devuelve su respuesta.</summary>
    Task<RespuestaLenguaje> CompletarAsync(PeticionLenguaje peticion, CancellationToken cancelacion = default);
}
