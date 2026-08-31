using System.Security.Cryptography;
using System.Text;

namespace MesaAyuda.Api.Modulos.Orquestacion;

/// <summary>
/// Verifica que un webhook provenga realmente del sistema que dice enviarlo.
///
/// Un webhook es una dirección pública: cualquiera que la conozca puede
/// enviarle datos. Sin verificación, cualquiera podría registrar solicitudes
/// falsas a nombre de terceros. La firma resuelve eso sin necesidad de
/// credenciales por usuario: quien envía calcula un código a partir del cuerpo
/// y de un secreto compartido, y quien recibe repite el cálculo y compara.
/// </summary>
public static class FirmaWebhook
{
    /// <summary>
    /// Calcula la firma HMAC-SHA256 de un cuerpo con el secreto indicado.
    /// </summary>
    public static string Calcular(string cuerpo, string secreto)
    {
        var clave = Encoding.UTF8.GetBytes(secreto);
        var datos = Encoding.UTF8.GetBytes(cuerpo);

        return Convert.ToHexString(HMACSHA256.HashData(clave, datos)).ToLowerInvariant();
    }

    /// <summary>
    /// Comprueba una firma recibida contra la esperada.
    /// </summary>
    public static bool EsValida(string cuerpo, string secreto, string? firmaRecibida)
    {
        if (string.IsNullOrWhiteSpace(firmaRecibida))
        {
            return false;
        }

        // Se admite el prefijo "sha256=" porque es la convención más extendida
        // y evita que un cambio de emisor rompa la integración.
        var limpia = firmaRecibida.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? firmaRecibida["sha256=".Length..]
            : firmaRecibida;

        var esperada = Calcular(cuerpo, secreto);

        // La comparación es de tiempo constante: comparar cadenas con el
        // operador habitual se detiene en el primer carácter distinto, y esa
        // diferencia de tiempo permite deducir la firma correcta byte a byte.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(limpia.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(esperada));
    }

    /// <summary>
    /// Resumen del contenido, usado para detectar reenvíos del mismo evento.
    /// </summary>
    public static string Huella(string cuerpo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cuerpo))).ToLowerInvariant();
}
