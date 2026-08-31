using System.Net;
using System.Text;
using System.Text.Json;
using MesaAyuda.Api.Nucleo.Configuracion;
using MesaAyuda.Api.Nucleo.Errores;
using MesaAyuda.Api.Nucleo.Proveedores;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MesaAyuda.Pruebas.Unidad;

public class ProveedorEmbeddingsLocalPruebas
{
    [Fact]
    public async Task ConstruyeLaPeticionQueEsperaElServicioLocal()
    {
        var manejador = new ManejadorControlado(RespuestaCon(1, Dimensiones.Estandar));

        await Construir(manejador).GenerarAsync("plazo de atención");

        Assert.EndsWith("api/embed", manejador.UltimaRuta);

        using var enviado = JsonDocument.Parse(manejador.UltimoCuerpo!);
        Assert.Equal("all-minilm", enviado.RootElement.GetProperty("model").GetString());
        Assert.Equal(1, enviado.RootElement.GetProperty("input").GetArrayLength());
    }

    [Fact]
    public async Task DevuelveUnVectorPorTextoEnviado()
    {
        var manejador = new ManejadorControlado(RespuestaCon(3, Dimensiones.Estandar));

        var lote = await Construir(manejador).GenerarLoteAsync(["uno", "dos", "tres"]);

        Assert.Equal(3, lote.Count);
        Assert.All(lote, v => Assert.Equal(Dimensiones.Estandar, v.Length));
    }

    [Fact]
    public async Task NormalizaLosVectoresRecibidos()
    {
        var manejador = new ManejadorControlado(RespuestaCon(1, Dimensiones.Estandar, valor: 4f));

        var vector = await Construir(manejador).GenerarAsync("texto");

        var longitud = Math.Sqrt(vector.Sum(v => (double)v * v));
        Assert.Equal(1d, longitud, precision: 4);
    }

    [Fact]
    public async Task UnaDimensionDistintaSeDetieneConUnMensajeQueIndicaQueHacer()
    {
        // Un modelo de 768 dimensiones fallaría al insertarse en una columna de
        // 384 con un error del motor difícil de interpretar. Se detiene antes.
        var manejador = new ManejadorControlado(RespuestaCon(1, 768));

        var error = await Assert.ThrowsAsync<ExcepcionDominio>(
            () => Construir(manejador).GenerarAsync("texto"));

        Assert.Contains("768", error.Message);
        Assert.Contains(Dimensiones.Estandar.ToString(), error.Message);
        Assert.Contains("migración", error.Message);
    }

    [Fact]
    public async Task UnaCantidadDistintaDeVectoresSeTrataComoFallo()
    {
        var manejador = new ManejadorControlado(RespuestaCon(2, Dimensiones.Estandar));

        await Assert.ThrowsAsync<ExcepcionDominio>(
            () => Construir(manejador).GenerarLoteAsync(["uno", "dos", "tres"]));
    }

    [Fact]
    public async Task UnServicioApagadoProduceUnErrorComprensible()
    {
        var manejador = new ManejadorControlado(excepcion: new HttpRequestException("sin conexión"));

        var error = await Assert.ThrowsAsync<ExcepcionDominio>(
            () => Construir(manejador).GenerarAsync("texto"));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, error.CodigoHttp);
    }

    [Fact]
    public async Task UnLoteVacioNoLlamaAlServicio()
    {
        var manejador = new ManejadorControlado(RespuestaCon(0, Dimensiones.Estandar));

        var resultado = await Construir(manejador).GenerarLoteAsync([]);

        Assert.Empty(resultado);
        Assert.Null(manejador.UltimoCuerpo);
    }

    [Fact]
    public void DeclaraLaDimensionEstandarDelSistema()
    {
        var proveedor = Construir(new ManejadorControlado("{}"));

        Assert.Equal(Dimensiones.Estandar, proveedor.Dimensiones);
        Assert.Equal("local", proveedor.Nombre);
    }

    private static string RespuestaCon(int cantidad, int dimensiones, float valor = 1f)
    {
        var vectores = Enumerable.Range(0, cantidad)
            .Select(_ => Enumerable.Repeat(valor, dimensiones).ToArray())
            .ToArray();

        return JsonSerializer.Serialize(new { embeddings = vectores });
    }

    private static ProveedorEmbeddingsLocal Construir(ManejadorControlado manejador)
    {
        var cliente = new HttpClient(manejador)
        {
            BaseAddress = new Uri("http://modelos:11434/")
        };

        var opciones = Options.Create(new OpcionesProveedores
        {
            Embeddings = "local",
            Local = new OpcionesLocal
            {
                UrlBase = "http://modelos:11434",
                ModeloEmbeddings = "all-minilm"
            }
        });

        return new ProveedorEmbeddingsLocal(cliente, opciones, NullLogger<ProveedorEmbeddingsLocal>.Instance);
    }

    private sealed class ManejadorControlado(
        string? respuesta = null,
        HttpStatusCode codigo = HttpStatusCode.OK,
        Exception? excepcion = null) : HttpMessageHandler
    {
        public string? UltimoCuerpo { get; private set; }

        public string UltimaRuta { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage peticion,
            CancellationToken cancelacion)
        {
            UltimaRuta = peticion.RequestUri!.ToString();

            if (peticion.Content is not null)
            {
                UltimoCuerpo = await peticion.Content.ReadAsStringAsync(cancelacion);
            }

            if (excepcion is not null)
            {
                throw excepcion;
            }

            return new HttpResponseMessage(codigo)
            {
                Content = new StringContent(respuesta ?? "{}", Encoding.UTF8, "application/json")
            };
        }
    }
}
