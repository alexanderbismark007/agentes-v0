using System.Text.Json;

namespace MesaAyuda.Api.Modulos.Agente.Herramientas;

/// <summary>
/// Riesgo asociado a ejecutar una herramienta. Determina si el agente puede
/// usarla por su cuenta o si necesita autorización de una persona.
/// </summary>
public enum NivelRiesgo
{
    /// <summary>Solo lee información. El agente la ejecuta libremente.</summary>
    Lectura = 1,

    /// <summary>Modifica datos. Requiere aprobación humana explícita.</summary>
    Escritura = 2
}

/// <summary>
/// Resultado de ejecutar una herramienta.
/// </summary>
/// <param name="Exitosa">Si la ejecución se completó sin errores.</param>
/// <param name="Contenido">Datos devueltos, en texto, para incorporar al razonamiento.</param>
/// <param name="Detalle">Información adicional para el registro de auditoría.</param>
public sealed record ResultadoHerramienta(bool Exitosa, string Contenido, string? Detalle = null)
{
    public static ResultadoHerramienta Correcta(string contenido, string? detalle = null) =>
        new(true, contenido, detalle);

    public static ResultadoHerramienta Fallida(string motivo) =>
        new(false, $"La herramienta no pudo completarse: {motivo}", motivo);
}

/// <summary>
/// Capacidad concreta que el agente puede invocar.
///
/// Es la diferencia entre un asistente y un agente: el asistente solo genera
/// texto; el agente elige, entre un conjunto acotado de herramientas, cuál
/// usar y con qué argumentos.
///
/// El conjunto es deliberadamente cerrado. El agente no puede ejecutar SQL
/// arbitrario ni llamar a servicios que no estén declarados aquí: solo puede
/// hacer aquello para lo que existe una herramienta, y cada herramienta valida
/// sus propios parámetros antes de tocar nada.
/// </summary>
public interface IHerramientaAgente
{
    /// <summary>Nombre con el que el agente la invoca. Debe ser estable.</summary>
    string Nombre { get; }

    /// <summary>Qué hace y cuándo conviene usarla. El agente decide leyendo esto.</summary>
    string Descripcion { get; }

    /// <summary>Nivel de riesgo, que define si necesita aprobación humana.</summary>
    NivelRiesgo Riesgo { get; }

    /// <summary>
    /// Parámetros que acepta, descritos como un esquema JSON. Se entrega al
    /// modelo para que sepa qué argumentos puede enviar.
    /// </summary>
    object EsquemaParametros { get; }

    /// <summary>
    /// Ejecuta la herramienta con los argumentos indicados.
    /// </summary>
    Task<ResultadoHerramienta> EjecutarAsync(JsonElement argumentos, CancellationToken cancelacion = default);
}

/// <summary>
/// Utilidades compartidas para leer los argumentos que envía el modelo.
///
/// Un modelo puede enviar un número como texto, omitir un parámetro opcional o
/// equivocarse en el tipo. Estas funciones absorben esas variaciones en un solo
/// lugar, en vez de repetir la defensa en cada herramienta.
/// </summary>
public static class Argumentos
{
    public static string? Texto(JsonElement argumentos, string nombre)
    {
        if (!argumentos.TryGetProperty(nombre, out var propiedad))
        {
            return null;
        }

        return propiedad.ValueKind switch
        {
            JsonValueKind.String => propiedad.GetString(),
            JsonValueKind.Number => propiedad.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => propiedad.GetRawText()
        };
    }

    public static int? Entero(JsonElement argumentos, string nombre)
    {
        if (!argumentos.TryGetProperty(nombre, out var propiedad))
        {
            return null;
        }

        return propiedad.ValueKind switch
        {
            JsonValueKind.Number when propiedad.TryGetInt32(out var valor) => valor,
            JsonValueKind.String when int.TryParse(propiedad.GetString(), out var valor) => valor,
            _ => null
        };
    }

    public static TEnum? Enumeracion<TEnum>(JsonElement argumentos, string nombre)
        where TEnum : struct, Enum
    {
        var texto = Texto(argumentos, nombre);

        return Enum.TryParse<TEnum>(texto, ignoreCase: true, out var valor) ? valor : null;
    }
}
