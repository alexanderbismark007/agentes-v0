using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MesaAyuda.Api.Nucleo.Configuracion;
using MesaAyuda.Api.Nucleo.Errores;
using Microsoft.Extensions.Options;

namespace MesaAyuda.Api.Nucleo.Proveedores;

/// <summary>
/// Proveedor que consulta un servicio de modelos alojado en la nube mediante el
/// formato de mensajes de la API de OpenAI, adoptado hoy por la mayoría de los
/// servicios equivalentes.
/// </summary>
public sealed class ProveedorNube(
    HttpClient cliente,
    IOptions<OpcionesProveedores> opciones,
    ILogger<ProveedorNube> registro) : IProveedorLenguaje
{
    private readonly OpcionesNube _configuracion = opciones.Value.Nube;

    public string Nombre => "nube";

    public async Task<RespuestaLenguaje> CompletarAsync(
        PeticionLenguaje peticion,
        CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(_configuracion.ClaveApi))
        {
            throw new ExcepcionDominio(
                "El proveedor 'nube' está activo pero no se configuró la clave de API. " +
                "Defina Proveedores__Nube__ClaveApi o cambie Proveedores__Lenguaje a 'simulado'.",
                StatusCodes.Status503ServiceUnavailable);
        }

        var cuerpo = new PeticionChat
        {
            Modelo = _configuracion.ModeloLenguaje,
            Temperatura = peticion.Temperatura,
            MaximoTokens = peticion.MaximoTokens,
            FormatoRespuesta = peticion.RespuestaJson ? new FormatoRespuesta("json_object") : null,
            Mensajes = peticion.Mensajes
                .Select(m => new MensajeChat(TraducirRol(m.Rol), m.Contenido))
                .ToArray()
        };

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _configuracion.ClaveApi);

        using var respuestaHttp = await cliente.PostAsJsonAsync(
            "chat/completions", cuerpo, OpcionesJson.Predeterminadas, cancelacion);

        if (!respuestaHttp.IsSuccessStatusCode)
        {
            var detalle = await respuestaHttp.Content.ReadAsStringAsync(cancelacion);
            registro.LogError("El proveedor de nube respondió {Codigo}: {Detalle}", (int)respuestaHttp.StatusCode, detalle);

            throw new ExcepcionDominio(
                $"El servicio de modelos respondió con código {(int)respuestaHttp.StatusCode}.",
                StatusCodes.Status502BadGateway);
        }

        var contenido = await respuestaHttp.Content.ReadFromJsonAsync<RespuestaChat>(
            OpcionesJson.Predeterminadas, cancelacion);

        var texto = contenido?.Opciones?.FirstOrDefault()?.Mensaje?.Contenido;
        if (string.IsNullOrWhiteSpace(texto))
        {
            throw new ExcepcionDominio(
                "El servicio de modelos devolvió una respuesta vacía.",
                StatusCodes.Status502BadGateway);
        }

        return new RespuestaLenguaje(
            texto,
            contenido!.Modelo ?? _configuracion.ModeloLenguaje,
            contenido.Uso?.TokensEntrada ?? 0,
            contenido.Uso?.TokensSalida ?? 0);
    }

    private static string TraducirRol(RolMensaje rol) => rol switch
    {
        RolMensaje.Sistema => "system",
        RolMensaje.Asistente => "assistant",
        _ => "user"
    };

    private sealed record PeticionChat
    {
        [JsonPropertyName("model")]
        public required string Modelo { get; init; }

        [JsonPropertyName("messages")]
        public required IReadOnlyList<MensajeChat> Mensajes { get; init; }

        [JsonPropertyName("temperature")]
        public double Temperatura { get; init; }

        [JsonPropertyName("max_tokens")]
        public int MaximoTokens { get; init; }

        [JsonPropertyName("response_format")]
        public FormatoRespuesta? FormatoRespuesta { get; init; }
    }

    private sealed record MensajeChat(
        [property: JsonPropertyName("role")] string Rol,
        [property: JsonPropertyName("content")] string Contenido);

    private sealed record FormatoRespuesta(
        [property: JsonPropertyName("type")] string Tipo);

    private sealed record RespuestaChat
    {
        [JsonPropertyName("model")]
        public string? Modelo { get; init; }

        [JsonPropertyName("choices")]
        public IReadOnlyList<OpcionChat>? Opciones { get; init; }

        [JsonPropertyName("usage")]
        public UsoTokens? Uso { get; init; }
    }

    private sealed record OpcionChat
    {
        [JsonPropertyName("message")]
        public MensajeChat? Mensaje { get; init; }
    }

    private sealed record UsoTokens
    {
        [JsonPropertyName("prompt_tokens")]
        public int TokensEntrada { get; init; }

        [JsonPropertyName("completion_tokens")]
        public int TokensSalida { get; init; }
    }
}
