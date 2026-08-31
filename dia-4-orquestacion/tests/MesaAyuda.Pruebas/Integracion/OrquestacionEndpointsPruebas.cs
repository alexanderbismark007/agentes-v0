using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using MesaAyuda.Api.Modulos.Orquestacion;
using MesaAyuda.Api.Modulos.Orquestacion.Contratos;
using MesaAyuda.Api.Modulos.Solicitudes.Contratos;

namespace MesaAyuda.Pruebas.Integracion;

public class OrquestacionEndpointsPruebas(FabricaPruebas fabrica) : IClassFixture<FabricaPruebas>
{
    private const string Ruta = "/api/v1/orquestacion/entrada/solicitudes";

    private const string CuerpoValido = """
        {
          "titulo": "No puedo acceder al correo institucional",
          "descripcion": "Olvidé la contraseña de mi usuario y la recuperación no llega a mi bandeja.",
          "solicitanteNombre": "Marcela Quispe",
          "solicitanteCorreo": "marcela.quispe@correo.upea.bo",
          "referenciaExterna": "ejecucion-1001",
          "origen": "n8n"
        }
        """;

    [Fact]
    public async Task UnaSolicitudEntranteSeRegistraYDevuelveSuCodigo()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var resultado = await EnviarAsync(cliente, CuerpoValido);

        Assert.False(resultado.Duplicado);
        Assert.StartsWith("SOL-", resultado.Codigo);
        Assert.Equal("Tecnologica", resultado.Categoria);
        Assert.Equal("Unidad de Sistemas", resultado.UnidadDestino);
        Assert.Equal("marcela.quispe@correo.upea.bo", resultado.CorreoDestinatario);
        Assert.Contains(resultado.Codigo, resultado.AsuntoRespuesta);
    }

    [Fact]
    public async Task ReenviarElMismoCuerpoNoDuplicaLaSolicitud()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var primero = await EnviarAsync(cliente, CuerpoValido);
        var segundo = await EnviarAsync(cliente, CuerpoValido);
        var tercero = await EnviarAsync(cliente, CuerpoValido);

        Assert.False(primero.Duplicado);
        Assert.True(segundo.Duplicado);
        Assert.True(tercero.Duplicado);

        Assert.Equal(primero.Codigo, segundo.Codigo);
        Assert.Equal(primero.Codigo, tercero.Codigo);

        // Lo decisivo: existe una sola solicitud, no tres.
        var pagina = await cliente.GetFromJsonAsync<PaginaRespuesta<SolicitudRespuesta>>("/api/v1/solicitudes");
        Assert.Equal(1, pagina!.Total);
    }

    [Fact]
    public async Task UnCuerpoDistintoSiCreaUnaSolicitudNueva()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var primero = await EnviarAsync(cliente, CuerpoValido);
        var segundo = await EnviarAsync(cliente, CuerpoValido.Replace("ejecucion-1001", "ejecucion-1002"));

        Assert.False(segundo.Duplicado);
        Assert.NotEqual(primero.Codigo, segundo.Codigo);
    }

    [Fact]
    public async Task ElAcuseSeRespaldaEnElReglamentoCuandoCorresponde()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        await IndexarAsync(cliente, "reglamento.md", """
            Artículo 8. Plazos especiales

            La reposición de credenciales de acceso a sistemas se resuelve en un día hábil. El certificado de notas se entrega en tres días hábiles.
            """);

        var resultado = await EnviarAsync(cliente, """
            {
              "titulo": "Solicito la reposicion de credenciales de acceso",
              "descripcion": "Necesito la reposición de mis credenciales de acceso a sistemas porque perdí la contraseña.",
              "solicitanteNombre": "Marcela Quispe",
              "solicitanteCorreo": "marcela.quispe@correo.upea.bo",
              "origen": "n8n"
            }
            """);

        Assert.True(resultado.RespuestaConRespaldo);
        Assert.Contains("Fuentes consultadas", resultado.CuerpoRespuesta);
        Assert.Contains("Artículo 8", resultado.CuerpoRespuesta);
    }

    [Fact]
    public async Task SinReglamentoElAcuseSoloConfirmaLaRecepcion()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var resultado = await EnviarAsync(cliente, CuerpoValido);

        Assert.False(resultado.RespuestaConRespaldo);
        Assert.Contains(resultado.Codigo, resultado.CuerpoRespuesta);
        Assert.Contains("será revisado por la unidad responsable", resultado.CuerpoRespuesta);

        // Sin respaldo no debe afirmar nada sobre el fondo del asunto.
        Assert.DoesNotContain("Según la normativa vigente", resultado.CuerpoRespuesta);
    }

    [Fact]
    public async Task LaReferenciaExternaQuedaEnElExpediente()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var resultado = await EnviarAsync(cliente, CuerpoValido);

        var comentarios = await cliente.GetFromJsonAsync<List<ComentarioRespuesta>>(
            $"/api/v1/solicitudes/{resultado.SolicitudId}/comentarios?incluirInternos=true");

        Assert.Contains(comentarios!, c => c.Contenido.Contains("ejecucion-1001"));
    }

    [Fact]
    public async Task CadaEventoQuedaAsentadoEnLaBitacora()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        await EnviarAsync(cliente, CuerpoValido);

        var eventos = await cliente.GetFromJsonAsync<PaginaRespuesta<EventoRespuesta>>(
            "/api/v1/orquestacion/eventos");

        Assert.Equal(1, eventos!.Total);

        var evento = eventos.Elementos.Single();
        Assert.Equal("solicitud.recibida", evento.Tipo);
        Assert.Equal("n8n", evento.Origen);
        Assert.Equal("Procesado", evento.Resultado);
        Assert.StartsWith("SOL-", evento.Referencia);
        Assert.Equal(1, evento.Intentos);
    }

    [Fact]
    public async Task LosDatosInvalidosSeRechazanYQuedanRegistrados()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var respuesta = await PublicarAsync(cliente, """
            {
              "titulo": "corto",
              "descripcion": "breve",
              "solicitanteNombre": "",
              "solicitanteCorreo": "no-es-correo",
              "origen": "n8n"
            }
            """);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var eventos = await cliente.GetFromJsonAsync<PaginaRespuesta<EventoRespuesta>>(
            "/api/v1/orquestacion/eventos?resultado=5");

        Assert.Equal(1, eventos!.Total);
        Assert.Equal("Rechazado", eventos.Elementos.Single().Resultado);
    }

    [Fact]
    public async Task UnCuerpoQueNoEsJsonSeRechazaConCodigoCuatrocientos()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var respuesta = await PublicarAsync(cliente, "esto no es json");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task LaSaludInformaLaTasaDeExito()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        await EnviarAsync(cliente, CuerpoValido);
        await EnviarAsync(cliente, CuerpoValido.Replace("1001", "1002"));

        var salud = await cliente.GetFromJsonAsync<SaludOrquestacion>("/api/v1/orquestacion/salud");

        Assert.Equal(2, salud!.TotalEventos);
        Assert.Equal(2, salud.Procesados);
        Assert.Equal(0, salud.Fallidos);
        Assert.Equal(100d, salud.TasaExito);
        Assert.NotNull(salud.UltimoEvento);
    }

    [Fact]
    public async Task LosDuplicadosNoCastiganLaTasaDeExito()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        await EnviarAsync(cliente, CuerpoValido);
        await EnviarAsync(cliente, CuerpoValido);

        var salud = await cliente.GetFromJsonAsync<SaludOrquestacion>("/api/v1/orquestacion/salud");

        // El reenvío no genera un evento nuevo ni degrada el indicador: un
        // reintento correcto no es una falla del procesamiento.
        Assert.Equal(100d, salud!.TasaExito);
        Assert.Equal(0, salud.Fallidos);
    }

    private static async Task<ResultadoIngreso> EnviarAsync(HttpClient cliente, string cuerpo)
    {
        var respuesta = await PublicarAsync(cliente, cuerpo);
        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<ResultadoIngreso>())!;
    }

    private static async Task<HttpResponseMessage> PublicarAsync(HttpClient cliente, string cuerpo)
    {
        using var contenido = new StringContent(cuerpo, Encoding.UTF8, "application/json");
        return await cliente.PostAsync(Ruta, contenido);
    }

    private static async Task IndexarAsync(HttpClient cliente, string nombre, string contenido)
    {
        using var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(Encoding.UTF8.GetBytes(contenido));
        archivo.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        formulario.Add(archivo, "archivo", nombre);

        var respuesta = await cliente.PostAsync("/api/v1/conocimiento/documentos?nivelAcceso=1", formulario);
        respuesta.EnsureSuccessStatusCode();
    }
}

/// <summary>
/// Pruebas del canal con la firma exigida, que requiere una fábrica propia
/// porque la exigencia se configura al arrancar la aplicación.
/// </summary>
public class OrquestacionFirmadaPruebas : IClassFixture<FabricaConFirma>
{
    private const string Ruta = "/api/v1/orquestacion/entrada/solicitudes";

    private const string Cuerpo = """
        {"titulo":"No puedo acceder al correo institucional","descripcion":"Olvidé la contraseña de mi usuario y no puedo entrar.","solicitanteNombre":"Marcela Quispe","solicitanteCorreo":"marcela.quispe@correo.upea.bo","origen":"n8n"}
        """;

    private readonly FabricaConFirma _fabrica;

    public OrquestacionFirmadaPruebas(FabricaConFirma fabrica) => _fabrica = fabrica;

    [Fact]
    public async Task SinFirmaLaPeticionSeRechaza()
    {
        var cliente = _fabrica.CrearClienteConBaseLimpia();

        using var contenido = new StringContent(Cuerpo, Encoding.UTF8, "application/json");
        var respuesta = await cliente.PostAsync(Ruta, contenido);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task ConFirmaValidaLaPeticionSeProcesa()
    {
        var cliente = _fabrica.CrearClienteConBaseLimpia();

        using var contenido = new StringContent(Cuerpo, Encoding.UTF8, "application/json");
        contenido.Headers.Add(
            OrquestacionEndpoints.CabeceraFirma,
            FirmaWebhook.Calcular(Cuerpo, FabricaConFirma.Secreto));

        var respuesta = await cliente.PostAsync(Ruta, contenido);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnCuerpoAlteradoDespuesDeFirmarSeRechaza()
    {
        var cliente = _fabrica.CrearClienteConBaseLimpia();

        var firma = FirmaWebhook.Calcular(Cuerpo, FabricaConFirma.Secreto);
        var alterado = Cuerpo.Replace("Marcela Quispe", "Otra Persona");

        using var contenido = new StringContent(alterado, Encoding.UTF8, "application/json");
        contenido.Headers.Add(OrquestacionEndpoints.CabeceraFirma, firma);

        var respuesta = await cliente.PostAsync(Ruta, contenido);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task ElIntentoRechazadoQuedaRegistrado()
    {
        var cliente = _fabrica.CrearClienteConBaseLimpia();

        using var contenido = new StringContent(Cuerpo, Encoding.UTF8, "application/json");
        await cliente.PostAsync(Ruta, contenido);

        var eventos = await cliente.GetFromJsonAsync<PaginaRespuesta<EventoRespuesta>>(
            "/api/v1/orquestacion/eventos?resultado=5");

        Assert.Equal(1, eventos!.Total);
        Assert.Contains("Firma", eventos.Elementos.Single().Detalle);
    }

    [Fact]
    public async Task LaSaludInformaQueLaFirmaEstaExigida()
    {
        var cliente = _fabrica.CrearClienteConBaseLimpia();

        var salud = await cliente.GetFromJsonAsync<SaludOrquestacion>("/api/v1/orquestacion/salud");

        Assert.True(salud!.FirmaExigida);
    }
}
