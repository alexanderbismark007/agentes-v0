using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MesaAyuda.Api.Nucleo.Configuracion;
using MesaAyuda.Api.Nucleo.Errores;
using Microsoft.Extensions.Options;

namespace MesaAyuda.Api.Nucleo.Proveedores;

/// <summary>
/// Proveedor de lenguaje servido por un modelo que corre en la propia
/// infraestructura, mediante la API nativa de Ollama.
///
/// Esta clase es todo lo que hizo falta agregar para que el sistema completo
/// funcione sin salir de la institución. Los módulos de los días 2, 3 y 4 no se
/// tocaron: dependen de <see cref="IProveedorLenguaje"/>, no de un servicio
/// concreto. Esa es la razón por la que la abstracción se definió el primer día
/// y no cuando hizo falta.
/// </summary>
public sealed class ProveedorLocal(
    HttpClient cliente,
    IOptions<OpcionesProveedores> opciones,
    ILogger<ProveedorLocal> registro) : IProveedorLenguaje
{
    private readonly OpcionesLocal _configuracion = opciones.Value.Local;

    public string Nombre => "local";

    public async Task<RespuestaLenguaje> CompletarAsync(
        PeticionLenguaje peticion,
        CancellationToken cancelacion = default)
    {
        var cuerpo = new PeticionChat
        {
            Modelo = _configuracion.ModeloLenguaje,
            Mensajes = peticion.Mensajes
                .Select(m => new MensajeChat(TraducirRol(m.Rol), m.Contenido))
                .ToArray(),

            // Sin transmisión progresiva: la respuesta llega completa en un
            // único documento, que es lo que espera el resto del sistema.
            Transmitir = false,

            // Ollama devuelve JSON estricto cuando se le indica este formato.
            Formato = peticion.RespuestaJson ? "json" : null,

            Opciones = new OpcionesModelo
            {
                Temperatura = peticion.Temperatura,
                MaximoTokens = peticion.MaximoTokens,

                // La ventana de contexto se fija explícitamente: el valor por
                // defecto de Ollama es corto y truncaría en silencio los
                // fragmentos que el módulo de conocimiento entrega.
                Contexto = _configuracion.VentanaContexto
            }
        };

        try
        {
            using var respuestaHttp = await cliente.PostAsJsonAsync(
                "api/chat", cuerpo, OpcionesJson.Predeterminadas, cancelacion);

            if (!respuestaHttp.IsSuccessStatusCode)
            {
                var detalle = await respuestaHttp.Content.ReadAsStringAsync(cancelacion);
                registro.LogError(
                    "El servicio local respondió {Codigo}: {Detalle}", (int)respuestaHttp.StatusCode, detalle);

                throw new ExcepcionDominio(
                    $"El servicio de modelos local respondió con código {(int)respuestaHttp.StatusCode}. " +
                    $"Verifique que el modelo '{_configuracion.ModeloLenguaje}' esté descargado.",
                    StatusCodes.Status502BadGateway);
            }

            var contenido = await respuestaHttp.Content.ReadFromJsonAsync<RespuestaChat>(
                OpcionesJson.Predeterminadas, cancelacion);

            var texto = contenido?.Mensaje?.Contenido;

            if (string.IsNullOrWhiteSpace(texto))
            {
                throw new ExcepcionDominio(
                    "El servicio de modelos local devolvió una respuesta vacía.",
                    StatusCodes.Status502BadGateway);
            }

            return new RespuestaLenguaje(
                texto,
                contenido!.Modelo ?? _configuracion.ModeloLenguaje,
                contenido.TokensEntrada,
                contenido.TokensSalida);
        }
        catch (HttpRequestException excepcion)
        {
            // Un modelo local no está "siempre disponible" como un servicio en
            // la nube: puede no estar levantado todavía o no tener el modelo.
            registro.LogError(excepcion, "No se pudo contactar al servicio de modelos local.");

            throw new ExcepcionDominio(
                $"No se pudo contactar al servicio de modelos local en {_configuracion.UrlBase}. " +
                "Verifique que el contenedor esté en ejecución.",
                StatusCodes.Status503ServiceUnavailable);
        }
        catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            throw new ExcepcionDominio(
                $"El modelo local superó el tiempo de espera de {_configuracion.TiempoEsperaSegundos} segundos. " +
                "En la primera consulta el modelo debe cargarse en memoria y puede tardar más.",
                StatusCodes.Status504GatewayTimeout);
        }
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

        [JsonPropertyName("stream")]
        public bool Transmitir { get; init; }

        [JsonPropertyName("format")]
        public string? Formato { get; init; }

        [JsonPropertyName("options")]
        public OpcionesModelo? Opciones { get; init; }
    }

    private sealed record MensajeChat(
        [property: JsonPropertyName("role")] string Rol,
        [property: JsonPropertyName("content")] string Contenido);

    private sealed record OpcionesModelo
    {
        [JsonPropertyName("temperature")]
        public double Temperatura { get; init; }

        [JsonPropertyName("num_predict")]
        public int MaximoTokens { get; init; }

        [JsonPropertyName("num_ctx")]
        public int Contexto { get; init; }
    }

    private sealed record RespuestaChat
    {
        [JsonPropertyName("model")]
        public string? Modelo { get; init; }

        [JsonPropertyName("message")]
        public MensajeChat? Mensaje { get; init; }

        [JsonPropertyName("prompt_eval_count")]
        public int TokensEntrada { get; init; }

        [JsonPropertyName("eval_count")]
        public int TokensSalida { get; init; }
    }
}
