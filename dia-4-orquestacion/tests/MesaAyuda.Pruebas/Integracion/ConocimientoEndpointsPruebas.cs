using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using MesaAyuda.Api.Modulos.Conocimiento.Contratos;

namespace MesaAyuda.Pruebas.Integracion;

public class ConocimientoEndpointsPruebas(FabricaPruebas fabrica) : IClassFixture<FabricaPruebas>
{
    private const string Reglamento = """
        Artículo 7. Plazo general

        El plazo general para resolver una solicitud es de diez días hábiles computados desde su registro.

        Artículo 8. Plazos especiales

        El certificado de notas se entrega en tres días hábiles. El certificado de egreso se entrega en cinco días hábiles. La reposición de credenciales de acceso a sistemas se resuelve en un día hábil.

        Artículo 17. Derecho a reclamo

        El solicitante puede presentar un reclamo dentro de los diez días hábiles siguientes a la notificación, cuando considere que la respuesta es incompleta, tardía o infundada.
        """;

    [Fact]
    public async Task IndexarUnDocumentoDevuelveLaCantidadDeFragmentos()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var resultado = await IndexarAsync(cliente, "reglamento.md", Reglamento);

        Assert.NotEqual(Guid.Empty, resultado.DocumentoId);
        Assert.True(resultado.Fragmentos > 0);
        Assert.False(resultado.SinCambios);
    }

    [Fact]
    public async Task ReindexarElMismoContenidoNoVuelveAProcesarlo()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var primera = await IndexarAsync(cliente, "reglamento.md", Reglamento);
        var segunda = await IndexarAsync(cliente, "reglamento.md", Reglamento);

        Assert.False(primera.SinCambios);
        Assert.True(segunda.SinCambios);
        Assert.Equal(primera.DocumentoId, segunda.DocumentoId);
        Assert.Equal(primera.Fragmentos, segunda.Fragmentos);
    }

    [Fact]
    public async Task CambiarElContenidoReemplazaLosFragmentos()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var primera = await IndexarAsync(cliente, "reglamento.md", Reglamento);
        var segunda = await IndexarAsync(cliente, "reglamento.md", Reglamento + "\n\nArtículo 25. Disposición nueva agregada al reglamento vigente para ampliarlo.");

        Assert.Equal(primera.DocumentoId, segunda.DocumentoId);
        Assert.False(segunda.SinCambios);

        var documentos = await cliente.GetFromJsonAsync<List<DocumentoRespuesta>>("/api/v1/conocimiento/documentos");
        Assert.Single(documentos!);
        Assert.Equal(segunda.Fragmentos, documentos![0].CantidadFragmentos);
    }

    [Fact]
    public async Task UnFormatoNoSoportadoEsRechazado()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var respuesta = await EnviarArchivoAsync(cliente, "planilla.xlsx", "contenido cualquiera");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task LaConsultaSeRespaldaEnFragmentosCitados()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        await IndexarAsync(cliente, "reglamento.md", Reglamento);

        var respuesta = await ConsultarAsync(cliente, "¿Cuál es el plazo general para resolver una solicitud?");

        Assert.True(respuesta.TieneRespaldo);
        Assert.NotEmpty(respuesta.Fuentes);
        Assert.Equal("simulado", respuesta.ProveedorEmbeddings);
        Assert.Equal("simulado", respuesta.ProveedorLenguaje);

        // Las fuentes se numeran desde uno para que la respuesta pueda citarlas.
        Assert.Equal(
            Enumerable.Range(1, respuesta.Fuentes.Count).ToArray(),
            respuesta.Fuentes.Select(f => f.Numero).ToArray());
    }

    [Fact]
    public async Task LaFuenteRecuperadaCorrespondeAlArticuloPreguntado()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        await IndexarAsync(cliente, "reglamento.md", Reglamento);

        var respuesta = await ConsultarAsync(cliente, "¿En cuántos días hábiles se entrega el certificado de notas?");

        Assert.True(respuesta.TieneRespaldo);

        // El fragmento más parecido debe ser el que habla de los certificados.
        var mejorFuente = respuesta.Fuentes.OrderByDescending(f => f.Similitud).First();
        Assert.Contains("certificado", mejorFuente.Extracto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SinDocumentosIndexadosSeInformaQueNoHayInformacion()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var respuesta = await ConsultarAsync(cliente, "¿Cuál es el plazo para presentar un reclamo?");

        Assert.False(respuesta.TieneRespaldo);
        Assert.Empty(respuesta.Fuentes);
        Assert.Equal(0d, respuesta.ConfianzaRecuperacion);
        Assert.Contains("No encontré información", respuesta.Respuesta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnaPreguntaSinRelacionConElDocumentoNoInventaRespuesta()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        await IndexarAsync(cliente, "reglamento.md", Reglamento);

        var respuesta = await ConsultarAsync(
            cliente,
            "¿Cuáles son las especificaciones técnicas del motor de una locomotora diésel?");

        Assert.False(respuesta.TieneRespaldo);
        Assert.Empty(respuesta.Fuentes);
    }

    [Fact]
    public async Task LosDocumentosInternosNoSeFiltranAConsultasPublicas()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        await EnviarArchivoAsync(
            cliente,
            "instructivo-interno.md",
            "Artículo 1. Procedimiento interno\n\nEl monto máximo autorizado para gastos de representación asciende a cinco mil bolivianos mensuales.",
            nivelAcceso: 2);

        var publica = await ConsultarAsync(cliente, "¿Cuál es el monto máximo para gastos de representación?", nivelAcceso: 1);
        var interna = await ConsultarAsync(cliente, "¿Cuál es el monto máximo para gastos de representación?", nivelAcceso: 2);

        Assert.False(publica.TieneRespaldo);
        Assert.Empty(publica.Fuentes);

        Assert.True(interna.TieneRespaldo);
        Assert.NotEmpty(interna.Fuentes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("corta")]
    public async Task UnaPreguntaInvalidaEsRechazada(string pregunta)
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/v1/conocimiento/consultas",
            new { pregunta, nivelAcceso = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task EliminarUnDocumentoLoQuitaDeLasConsultas()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        var indexado = await IndexarAsync(cliente, "reglamento.md", Reglamento);

        var conDocumento = await ConsultarAsync(cliente, "¿Cuál es el plazo general para resolver una solicitud?");
        Assert.True(conDocumento.TieneRespaldo);

        var borrado = await cliente.DeleteAsync($"/api/v1/conocimiento/documentos/{indexado.DocumentoId}");
        Assert.Equal(HttpStatusCode.NoContent, borrado.StatusCode);

        var sinDocumento = await ConsultarAsync(cliente, "¿Cuál es el plazo general para resolver una solicitud?");
        Assert.False(sinDocumento.TieneRespaldo);
    }

    [Fact]
    public async Task EliminarUnDocumentoInexistenteDevuelveNoEncontrado()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var respuesta = await cliente.DeleteAsync($"/api/v1/conocimiento/documentos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    private static async Task<ResultadoIndexacion> IndexarAsync(HttpClient cliente, string nombre, string contenido)
    {
        var respuesta = await EnviarArchivoAsync(cliente, nombre, contenido);
        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<ResultadoIndexacion>())!;
    }

    private static async Task<HttpResponseMessage> EnviarArchivoAsync(
        HttpClient cliente,
        string nombre,
        string contenido,
        int nivelAcceso = 1)
    {
        using var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(Encoding.UTF8.GetBytes(contenido));
        archivo.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        formulario.Add(archivo, "archivo", nombre);

        return await cliente.PostAsync($"/api/v1/conocimiento/documentos?nivelAcceso={nivelAcceso}", formulario);
    }

    private static async Task<RespuestaConsulta> ConsultarAsync(
        HttpClient cliente,
        string pregunta,
        int nivelAcceso = 1)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/v1/conocimiento/consultas",
            new { pregunta, nivelAcceso });

        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<RespuestaConsulta>())!;
    }
}
