namespace MesaAyuda.Api.Nucleo.Configuracion;

/// <summary>
/// Configuración del canal por el que entra la automatización externa.
/// </summary>
public sealed class OpcionesOrquestacion
{
    public const string Seccion = "Orquestacion";

    /// <summary>
    /// Exige que cada webhook llegue firmado. En desarrollo viene desactivado
    /// para poder probar con herramientas simples; en cualquier despliegue real
    /// debe estar activo, porque la dirección del webhook es pública.
    /// </summary>
    public bool ExigirFirma { get; set; }

    /// <summary>
    /// Secreto compartido con el sistema que envía los eventos. Nunca debe
    /// versionarse: se define por variable de entorno.
    /// </summary>
    public string SecretoWebhook { get; set; } = string.Empty;
}
