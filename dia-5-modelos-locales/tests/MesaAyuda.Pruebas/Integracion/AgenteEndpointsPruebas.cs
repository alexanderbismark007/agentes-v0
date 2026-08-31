using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using MesaAyuda.Api.Dominio.Solicitudes;
using MesaAyuda.Api.Modulos.Agente.Contratos;
using MesaAyuda.Api.Modulos.Solicitudes.Contratos;

namespace MesaAyuda.Pruebas.Integracion;

public class AgenteEndpointsPruebas(FabricaPruebas fabrica) : IClassFixture<FabricaPruebas>
{
    private const string Reglamento = """
        Artículo 7. Plazo general

        El plazo general para resolver una solicitud es de diez días hábiles computados desde su registro.

        Artículo 20. Conservación

        Los expedientes de solicitudes se conservan por un período de cinco años contados desde su cierre.
        """;

    [Fact]
    public async Task ElCatalogoDeclaraQueHerramientasRequierenAprobacion()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var catalogo = await cliente.GetFromJsonAsync<List<HerramientaDisponible>>("/api/v1/agente/herramientas");

        Assert.NotNull(catalogo);
        Assert.Equal(5, catalogo!.Count);

        var lectura = catalogo.Where(h => !h.RequiereAprobacion).ToArray();
        var escritura = catalogo.Where(h => h.RequiereAprobacion).ToArray();

        Assert.Equal(4, lectura.Length);
        Assert.All(lectura, h => Assert.Equal("Lectura", h.Riesgo));

        // La única que modifica datos es el cambio de estado.
        Assert.Single(escritura);
        Assert.Equal("cambiar_estado_solicitud", escritura[0].Nombre);
    }

    [Fact]
    public async Task ElAgenteEligeLaHerramientaDeEstadisticasParaPreguntasDeVolumen()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        await CrearSolicitudAsync(cliente);

        var respuesta = await ConsultarAsync(cliente, "¿Cuál es el resumen de indicadores de la mesa de ayuda?");

        Assert.NotEmpty(respuesta.Pasos);
        Assert.Equal("obtener_estadisticas", respuesta.Pasos.First().Herramienta);
        Assert.True(respuesta.Pasos.First().Exitosa);
        Assert.False(respuesta.AlcanzoLimite);
    }

    [Fact]
    public async Task ElAgenteEligeElReglamentoParaPreguntasNormativas()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        await IndexarAsync(cliente, "reglamento.md", Reglamento);

        var respuesta = await ConsultarAsync(cliente, "¿Qué plazo establece el reglamento para resolver una solicitud?");

        Assert.NotEmpty(respuesta.Pasos);
        Assert.Equal("consultar_reglamento", respuesta.Pasos.First().Herramienta);
        Assert.Contains("diez días hábiles", respuesta.Respuesta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ElAgenteEligeElDetalleCuandoLaPreguntaTraeUnCodigo()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        var creada = await CrearSolicitudAsync(cliente);

        var respuesta = await ConsultarAsync(cliente, $"Dame el detalle de la solicitud {creada.Codigo}");

        Assert.NotEmpty(respuesta.Pasos);
        Assert.Equal("ver_solicitud", respuesta.Pasos.First().Herramienta);
        Assert.Contains(creada.Codigo, respuesta.Respuesta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ElAgenteBuscaSolicitudesFiltrandoPorLosTerminosDeLaPregunta()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        await CrearSolicitudAsync(cliente);

        var respuesta = await ConsultarAsync(cliente, "Muéstrame las solicitudes tecnológicas registradas");

        Assert.NotEmpty(respuesta.Pasos);

        var paso = respuesta.Pasos.First();
        Assert.Equal("buscar_solicitudes", paso.Herramienta);
        Assert.Contains("Tecnologica", paso.Argumentos, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CadaPasoQuedaRegistradoEnLaTraza()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        await CrearSolicitudAsync(cliente);

        var respuesta = await ConsultarAsync(cliente, "¿Cuántas solicitudes hay en total?");

        Assert.All(respuesta.Pasos, paso =>
        {
            Assert.False(string.IsNullOrWhiteSpace(paso.Herramienta));
            Assert.False(string.IsNullOrWhiteSpace(paso.Argumentos));
            Assert.True(paso.Numero > 0);
            Assert.True(paso.Milisegundos >= 0);
        });

        Assert.Equal(
            Enumerable.Range(1, respuesta.Pasos.Count).ToArray(),
            respuesta.Pasos.Select(p => p.Numero).ToArray());
    }

    [Fact]
    public async Task SinAutorizacionElAgenteNoModificaDatos()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        var creada = await CrearSolicitudAsync(cliente);

        await ConsultarAsync(
            cliente,
            $"Cambia el estado de la solicitud {creada.Codigo} a EnRevision",
            autorizarEscritura: false);

        // Lo decisivo no es qué respondió, sino que la solicitud no cambió.
        var actual = await cliente.GetFromJsonAsync<SolicitudRespuesta>($"/api/v1/solicitudes/{creada.Id}");

        Assert.Equal("Recibida", actual!.Estado);
    }

    [Fact]
    public async Task LaHerramientaDeEscrituraNoSeEjecutaAunqueElModeloLaPida()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        var creada = await CrearSolicitudAsync(cliente);

        var respuesta = await ConsultarAsync(
            cliente,
            $"Cambia el estado de la solicitud {creada.Codigo} a EnRevision",
            autorizarEscritura: false);

        // El modelo sí propuso la acción; el agente la retuvo. La barrera está
        // en el código, no en la instrucción que se le da al modelo.
        Assert.DoesNotContain(respuesta.Pasos, p => p.Herramienta == "cambiar_estado_solicitud");
        Assert.Single(respuesta.AccionesPendientes);

        var pendiente = respuesta.AccionesPendientes.Single();
        Assert.Equal("cambiar_estado_solicitud", pendiente.Herramienta);
        Assert.Contains(creada.Codigo, pendiente.Argumentos);
        Assert.Contains("aprobación", pendiente.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConAutorizacionLaMismaAccionSiSeEjecuta()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        var creada = await CrearSolicitudAsync(cliente);

        var respuesta = await ConsultarAsync(
            cliente,
            $"Cambia el estado de la solicitud {creada.Codigo} a EnRevision",
            autorizarEscritura: true);

        Assert.Empty(respuesta.AccionesPendientes);
        Assert.Contains(respuesta.Pasos, p => p.Herramienta == "cambiar_estado_solicitud" && p.Exitosa);

        var actual = await cliente.GetFromJsonAsync<SolicitudRespuesta>($"/api/v1/solicitudes/{creada.Id}");
        Assert.Equal("EnRevision", actual!.Estado);
    }

    [Fact]
    public async Task ElDominioSigueMandandoAunqueElOperadorAutorice()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        var creada = await CrearSolicitudAsync(cliente);

        // Recibida no puede saltar directamente a Cerrada, ni siquiera con
        // autorización: la máquina de estados es la última palabra.
        var respuesta = await ConsultarAsync(
            cliente,
            $"Cambia el estado de la solicitud {creada.Codigo} a Cerrada",
            autorizarEscritura: true);

        Assert.Contains(respuesta.Pasos, p => p.Herramienta == "cambiar_estado_solicitud" && !p.Exitosa);

        var actual = await cliente.GetFromJsonAsync<SolicitudRespuesta>($"/api/v1/solicitudes/{creada.Id}");
        Assert.Equal("Recibida", actual!.Estado);
    }

    [Fact]
    public async Task LaTrazaNuncaOmiteUnaHerramientaEjecutada()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        await CrearSolicitudAsync(cliente);

        var respuesta = await ConsultarAsync(cliente, "¿Cuál es la distribución por categoría?");

        // Toda ejecución deja rastro: no puede haber respuesta apoyada en datos
        // sin que la traza indique de dónde salieron.
        Assert.NotEmpty(respuesta.Pasos);
        Assert.Equal("simulado", respuesta.ProveedorLenguaje);
        Assert.True(respuesta.MilisegundosTotales >= 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("corta")]
    public async Task UnaPreguntaInvalidaEsRechazada(string pregunta)
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/v1/agente/consultas",
            new { pregunta, autorizarEscritura = false });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task ElReporteEjecutivoSeApoyaEnDatosVerificables()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        await CrearSolicitudAsync(cliente);

        var reporte = await PostReporteAsync(cliente);

        Assert.NotNull(reporte);
        Assert.False(string.IsNullOrWhiteSpace(reporte.Contenido));
        Assert.Equal(2, reporte.DatosConsultados.Count);
        Assert.All(reporte.DatosConsultados, d => Assert.True(d.Exitosa));
    }

    private static async Task<ReporteEjecutivo> PostReporteAsync(HttpClient cliente)
    {
        var respuesta = await cliente.PostAsync("/api/v1/agente/reportes/ejecutivo", content: null);
        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<ReporteEjecutivo>())!;
    }

    private static async Task<RespuestaAgente> ConsultarAsync(
        HttpClient cliente,
        string pregunta,
        bool autorizarEscritura = false)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/v1/agente/consultas",
            new { pregunta, autorizarEscritura });

        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<RespuestaAgente>())!;
    }

    private static async Task<SolicitudRespuesta> CrearSolicitudAsync(HttpClient cliente)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/solicitudes", new CrearSolicitud
        {
            Titulo = "No puedo acceder al correo institucional",
            Descripcion = "Olvidé la contraseña de mi usuario y la recuperación no llega a mi bandeja.",
            SolicitanteNombre = "Persona Solicitante",
            SolicitanteCorreo = "persona@correo.upea.bo",
            Prioridad = PrioridadSolicitud.Alta
        });

        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<SolicitudRespuesta>())!;
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
