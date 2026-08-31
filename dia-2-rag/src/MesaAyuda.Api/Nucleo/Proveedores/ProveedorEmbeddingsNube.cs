using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MesaAyuda.Api.Nucleo.Configuracion;
using MesaAyuda.Api.Nucleo.Errores;
using Microsoft.Extensions.Options;

namespace MesaAyuda.Api.Nucleo.Proveedores;

/// <summary>
/// Obtiene los vectores de un servicio externo de embeddings.
/// </summary>
public sealed class ProveedorEmbeddingsNube(
    HttpClient cliente,
    IOptions<OpcionesProveedores> opciones,
    ILogger<ProveedorEmbeddingsNube> registro) : IProveedorEmbeddings
{
    /// <summary>
    /// Tope de textos por llamada. Enviar un documento entero en una sola
    /// petición suele superar los límites del servicio.
    /// </summary>
    private const int TamanoLote = 64;

    private readonly OpcionesNube _configuracion = opciones.Value.Nube;

    public string Nombre => "nube";

    public int Dimensiones => _configuracion.DimensionesEmbedding;

    public async Task<float[]> GenerarAsync(string texto, CancellationToken cancelacion = default)
    {
        var resultado = await GenerarLoteAsync([texto], cancelacion);
        return resultado[0];
    }

    public async Task<IReadOnlyList<float[]>> GenerarLoteAsync(
        IReadOnlyList<string> textos,
        CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(_configuracion.ClaveApi))
        {
            throw new ExcepcionDominio(
                "El proveedor de embeddings 'nube' está activo pero no se configuró la clave de API. " +
                "Defina Proveedores__Nube__ClaveApi o cambie Proveedores__Embeddings a 'simulado'.",
                StatusCodes.Status503ServiceUnavailable);
        }

        if (textos.Count == 0)
        {
            return [];
        }

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _configuracion.ClaveApi);

        var vectores = new List<float[]>(textos.Count);

        foreach (var lote in Particionar(textos, TamanoLote))
        {
            vectores.AddRange(await SolicitarLoteAsync(lote, cancelacion));
        }

        return vectores;
    }

    private async Task<IReadOnlyList<float[]>> SolicitarLoteAsync(
        IReadOnlyList<string> lote,
        CancellationToken cancelacion)
    {
        var cuerpo = new PeticionEmbeddings
        {
            Modelo = _configuracion.ModeloEmbeddings,
            Entrada = lote,

            // Se pide explícitamente el tamaño estándar del sistema. Sin esto el
            // servicio devuelve su dimensión nativa, que no entra en la columna
            // vectorial ya definida.
            Dimensiones = _configuracion.DimensionesEmbedding
        };

        using var respuestaHttp = await cliente.PostAsJsonAsync(
            "embeddings", cuerpo, OpcionesJson.Predeterminadas, cancelacion);

        if (!respuestaHttp.IsSuccessStatusCode)
        {
            var detalle = await respuestaHttp.Content.ReadAsStringAsync(cancelacion);
            registro.LogError("El servicio de embeddings respondió {Codigo}: {Detalle}", (int)respuestaHttp.StatusCode, detalle);

            throw new ExcepcionDominio(
                $"El servicio de embeddings respondió con código {(int)respuestaHttp.StatusCode}.",
                StatusCodes.Status502BadGateway);
        }

        var contenido = await respuestaHttp.Content.ReadFromJsonAsync<RespuestaEmbeddings>(
            OpcionesJson.Predeterminadas, cancelacion);

        if (contenido?.Datos is null || contenido.Datos.Count != lote.Count)
        {
            throw new ExcepcionDominio(
                "El servicio de embeddings devolvió una cantidad de vectores distinta a la solicitada.",
                StatusCodes.Status502BadGateway);
        }

        // El servicio no garantiza el orden de la respuesta, así que se reordena
        // por el índice que él mismo informa antes de devolver los vectores.
        return contenido.Datos
            .OrderBy(d => d.Indice)
            .Select(d => Vectores.Normalizar(d.Vector ?? []))
            .ToArray();
    }

    private static IEnumerable<IReadOnlyList<T>> Particionar<T>(IReadOnlyList<T> origen, int tamano)
    {
        for (var inicio = 0; inicio < origen.Count; inicio += tamano)
        {
            yield return origen.Skip(inicio).Take(tamano).ToArray();
        }
    }

    private sealed record PeticionEmbeddings
    {
        [JsonPropertyName("model")]
        public required string Modelo { get; init; }

        [JsonPropertyName("input")]
        public required IReadOnlyList<string> Entrada { get; init; }

        [JsonPropertyName("dimensions")]
        public int Dimensiones { get; init; }
    }

    private sealed record RespuestaEmbeddings
    {
        [JsonPropertyName("data")]
        public IReadOnlyList<DatoEmbedding>? Datos { get; init; }
    }

    private sealed record DatoEmbedding
    {
        [JsonPropertyName("index")]
        public int Indice { get; init; }

        [JsonPropertyName("embedding")]
        public float[]? Vector { get; init; }
    }
}
