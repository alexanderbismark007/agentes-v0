using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MesaAyuda.Api.Nucleo.Configuracion;
using MesaAyuda.Api.Nucleo.Errores;
using Microsoft.Extensions.Options;

namespace MesaAyuda.Api.Nucleo.Proveedores;

/// <summary>
/// Genera embeddings con un modelo local servido por Ollama.
///
/// El modelo por defecto produce vectores de 384 dimensiones, que es
/// exactamente el tamaño con el que se definió la columna vectorial el día 2.
/// Por eso migrar a local no obliga a cambiar el esquema de la base de datos:
/// basta con reindexar los documentos, porque los vectores de un modelo no son
/// comparables con los de otro.
/// </summary>
public sealed class ProveedorEmbeddingsLocal(
    HttpClient cliente,
    IOptions<OpcionesProveedores> opciones,
    ILogger<ProveedorEmbeddingsLocal> registro) : IProveedorEmbeddings
{
    private readonly OpcionesLocal _configuracion = opciones.Value.Local;

    public string Nombre => "local";

    public int Dimensiones => Nucleo.Configuracion.Dimensiones.Estandar;

    public async Task<float[]> GenerarAsync(string texto, CancellationToken cancelacion = default)
    {
        var resultado = await GenerarLoteAsync([texto], cancelacion);
        return resultado[0];
    }

    public async Task<IReadOnlyList<float[]>> GenerarLoteAsync(
        IReadOnlyList<string> textos,
        CancellationToken cancelacion = default)
    {
        if (textos.Count == 0)
        {
            return [];
        }

        var cuerpo = new PeticionEmbeddings
        {
            Modelo = _configuracion.ModeloEmbeddings,
            Entrada = textos
        };

        try
        {
            using var respuestaHttp = await cliente.PostAsJsonAsync(
                "api/embed", cuerpo, OpcionesJson.Predeterminadas, cancelacion);

            if (!respuestaHttp.IsSuccessStatusCode)
            {
                var detalle = await respuestaHttp.Content.ReadAsStringAsync(cancelacion);
                registro.LogError(
                    "El servicio local de embeddings respondió {Codigo}: {Detalle}",
                    (int)respuestaHttp.StatusCode, detalle);

                throw new ExcepcionDominio(
                    $"El servicio local de embeddings respondió con código {(int)respuestaHttp.StatusCode}. " +
                    $"Verifique que el modelo '{_configuracion.ModeloEmbeddings}' esté descargado.",
                    StatusCodes.Status502BadGateway);
            }

            var contenido = await respuestaHttp.Content.ReadFromJsonAsync<RespuestaEmbeddings>(
                OpcionesJson.Predeterminadas, cancelacion);

            if (contenido?.Vectores is null || contenido.Vectores.Count != textos.Count)
            {
                throw new ExcepcionDominio(
                    "El servicio local de embeddings devolvió una cantidad de vectores distinta a la solicitada.",
                    StatusCodes.Status502BadGateway);
            }

            VerificarDimension(contenido.Vectores[0].Length);

            return contenido.Vectores.Select(Vectores.Normalizar).ToArray();
        }
        catch (HttpRequestException excepcion)
        {
            registro.LogError(excepcion, "No se pudo contactar al servicio local de embeddings.");

            throw new ExcepcionDominio(
                $"No se pudo contactar al servicio de modelos local en {_configuracion.UrlBase}. " +
                "Verifique que el contenedor esté en ejecución.",
                StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>
    /// Comprueba que el modelo produzca la dimensión esperada.
    ///
    /// Si no coincide, la inserción fallaría más adelante con un error del
    /// motor de base de datos difícil de interpretar. Es preferible detenerse
    /// aquí con un mensaje que diga exactamente qué hacer.
    /// </summary>
    private void VerificarDimension(int recibida)
    {
        if (recibida == Dimensiones)
        {
            return;
        }

        throw new ExcepcionDominio(
            $"El modelo local '{_configuracion.ModeloEmbeddings}' produce vectores de {recibida} dimensiones, " +
            $"pero el sistema almacena vectores de {Dimensiones}. " +
            "Use un modelo de la dimensión esperada o cambie Dimensiones.Estandar y genere una migración nueva.",
            StatusCodes.Status500InternalServerError);
    }

    private sealed record PeticionEmbeddings
    {
        [JsonPropertyName("model")]
        public required string Modelo { get; init; }

        [JsonPropertyName("input")]
        public required IReadOnlyList<string> Entrada { get; init; }
    }

    private sealed record RespuestaEmbeddings
    {
        [JsonPropertyName("embeddings")]
        public IReadOnlyList<float[]>? Vectores { get; init; }
    }
}
