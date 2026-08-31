using System.Net;
using System.Text;
using System.Text.Json;
using MesaAyuda.Api.Nucleo.Configuracion;
using MesaAyuda.Api.Nucleo.Errores;
using MesaAyuda.Api.Nucleo.Proveedores;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MesaAyuda.Pruebas.Unidad;

/// <summary>
/// Verifica el contrato con el servicio de modelos local sin necesitar que
/// Ollama esté instalado: se sustituye el transporte HTTP por uno controlado.
///
/// Lo que se comprueba no es que el modelo responda bien (eso depende del
/// modelo), sino que el adaptador construya la petición correcta, interprete la
/// respuesta y traduzca cada falla a un error comprensible.
/// </summary>
public class ProveedorLocalPruebas
{
    private static readonly PeticionLenguaje Peticion = new()
    {
        Mensajes =
        [
            MensajeLenguaje.Sistema("Eres un asistente."),
            MensajeLenguaje.Usuario("¿Cuál es el plazo?")
        ],
        Temperatura = 0d,
        MaximoTokens = 500
    };

    [Fact]
    public async Task ConstruyeLaPeticionQueEsperaElServicioLocal()
    {
        var manejador = new ManejadorControlado(
            """{"model":"llama3.2:3b","message":{"role":"assistant","content":"Diez días hábiles."},"prompt_eval_count":40,"eval_count":8}""");

        var proveedor = Construir(manejador);
        await proveedor.CompletarAsync(Peticion);

        Assert.EndsWith("api/chat", manejador.UltimaRuta);

        using var enviado = JsonDocument.Parse(manejador.UltimoCuerpo!);
        var raiz = enviado.RootElement;

        Assert.Equal("llama3.2:3b", raiz.GetProperty("model").GetString());
        Assert.False(raiz.GetProperty("stream").GetBoolean());

        // Los roles se traducen al vocabulario del servicio.
        var mensajes = raiz.GetProperty("messages");
        Assert.Equal("system", mensajes[0].GetProperty("role").GetString());
        Assert.Equal("user", mensajes[1].GetProperty("role").GetString());

        // La ventana de contexto se envía explícitamente: sin esto, el valor
        // por defecto truncaría los fragmentos recuperados.
        Assert.Equal(4096, raiz.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.Equal(500, raiz.GetProperty("options").GetProperty("num_predict").GetInt32());
    }

    [Fact]
    public async Task InterpretaLaRespuestaYRegistraElConsumo()
    {
        var manejador = new ManejadorControlado(
            """{"model":"llama3.2:3b","message":{"role":"assistant","content":"Diez días hábiles."},"prompt_eval_count":40,"eval_count":8}""");

        var respuesta = await Construir(manejador).CompletarAsync(Peticion);

        Assert.Equal("Diez días hábiles.", respuesta.Contenido);
        Assert.Equal("llama3.2:3b", respuesta.Modelo);
        Assert.Equal(40, respuesta.TokensEntrada);
        Assert.Equal(8, respuesta.TokensSalida);
    }

    [Fact]
    public async Task SolicitaFormatoJsonCuandoSeLePide()
    {
        var manejador = new ManejadorControlado(
            """{"message":{"role":"assistant","content":"{\"categoria\":\"Otra\"}"}}""");

        await Construir(manejador).CompletarAsync(Peticion with { RespuestaJson = true });

        using var enviado = JsonDocument.Parse(manejador.UltimoCuerpo!);
        Assert.Equal("json", enviado.RootElement.GetProperty("format").GetString());
    }

    [Fact]
    public async Task NoEnviaFormatoCuandoSeEsperaTextoLibre()
    {
        var manejador = new ManejadorControlado(
            """{"message":{"role":"assistant","content":"texto"}}""");

        await Construir(manejador).CompletarAsync(Peticion);

        using var enviado = JsonDocument.Parse(manejador.UltimoCuerpo!);
        Assert.False(enviado.RootElement.TryGetProperty("format", out _));
    }

    [Fact]
    public async Task UnServicioApagadoProduceUnErrorComprensible()
    {
        var proveedor = Construir(new ManejadorControlado(excepcion: new HttpRequestException("sin conexión")));

        var error = await Assert.ThrowsAsync<ExcepcionDominio>(() => proveedor.CompletarAsync(Peticion));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, error.CodigoHttp);
        Assert.Contains("contenedor esté en ejecución", error.Message);
    }

    [Fact]
    public async Task UnModeloNoDescargadoProduceUnErrorQueLoIndica()
    {
        var manejador = new ManejadorControlado(
            """{"error":"model 'llama3.2:3b' not found"}""",
            HttpStatusCode.NotFound);

        var error = await Assert.ThrowsAsync<ExcepcionDominio>(() => Construir(manejador).CompletarAsync(Peticion));

        Assert.Equal(StatusCodes.Status502BadGateway, error.CodigoHttp);
        Assert.Contains("descargado", error.Message);
    }

    [Fact]
    public async Task UnaRespuestaVaciaSeTrataComoFalloDelServicio()
    {
        var manejador = new ManejadorControlado("""{"message":{"role":"assistant","content":""}}""");

        var error = await Assert.ThrowsAsync<ExcepcionDominio>(() => Construir(manejador).CompletarAsync(Peticion));

        Assert.Equal(StatusCodes.Status502BadGateway, error.CodigoHttp);
    }

    [Fact]
    public void ElProveedorSeIdentificaComoLocal()
    {
        Assert.Equal("local", Construir(new ManejadorControlado("{}")).Nombre);
    }

    private static ProveedorLocal Construir(ManejadorControlado manejador)
    {
        var cliente = new HttpClient(manejador)
        {
            BaseAddress = new Uri("http://modelos:11434/")
        };

        var opciones = Options.Create(new OpcionesProveedores
        {
            Lenguaje = "local",
            Local = new OpcionesLocal
            {
                UrlBase = "http://modelos:11434",
                ModeloLenguaje = "llama3.2:3b",
                VentanaContexto = 4096
            }
        });

        return new ProveedorLocal(cliente, opciones, NullLogger<ProveedorLocal>.Instance);
    }

    /// <summary>
    /// Transporte HTTP sustituto que devuelve lo que la prueba indique y
    /// conserva lo que se le envió, para poder inspeccionarlo.
    /// </summary>
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
