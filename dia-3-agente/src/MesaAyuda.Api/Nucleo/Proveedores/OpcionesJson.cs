using System.Text.Json;
using System.Text.Json.Serialization;

namespace MesaAyuda.Api.Nucleo.Proveedores;

/// <summary>
/// Configuración de serialización compartida por los proveedores, para que
/// todos produzcan y consuman JSON con el mismo criterio.
/// </summary>
public static class OpcionesJson
{
    public static readonly JsonSerializerOptions Predeterminadas = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
