using System.Net;
using System.Net.Http.Json;
using MesaAyuda.Api.Dominio.Solicitudes;
using MesaAyuda.Api.Modulos.Solicitudes;
using MesaAyuda.Api.Modulos.Solicitudes.Contratos;

namespace MesaAyuda.Pruebas.Integracion;

public class SolicitudesEndpointsPruebas(FabricaPruebas fabrica) : IClassFixture<FabricaPruebas>
{
    private static readonly CrearSolicitud SolicitudValida = new()
    {
        Titulo = "No puedo acceder al correo institucional",
        Descripcion = "Olvidé la contraseña de mi usuario y la recuperación no llega a mi bandeja de entrada.",
        SolicitanteNombre = "Persona Solicitante",
        SolicitanteCorreo = "persona@correo.upea.bo"
    };

    [Fact]
    public async Task RegistrarUnaSolicitudDevuelveCreadaConCodigoCorrelativo()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/solicitudes", SolicitudValida);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var creada = await respuesta.Content.ReadFromJsonAsync<SolicitudRespuesta>();

        Assert.NotNull(creada);
        Assert.StartsWith("SOL-", creada!.Codigo);
        Assert.Equal("Recibida", creada.Estado);
        Assert.Equal(SolicitudValida.SolicitanteCorreo, creada.SolicitanteCorreo);
        Assert.Contains("/api/v1/solicitudes/", respuesta.Headers.Location!.ToString());
    }

    [Fact]
    public async Task ElClasificadorSugiereLaCategoriaYSeGuardaSuTrazabilidad()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/solicitudes", SolicitudValida);
        var creada = await respuesta.Content.ReadFromJsonAsync<SolicitudRespuesta>();

        Assert.NotNull(creada);

        // El texto habla de contraseña y usuario: corresponde a la categoría tecnológica.
        Assert.Equal("Tecnologica", creada!.CategoriaSugerida);
        Assert.Equal("Tecnologica", creada.Categoria);
        Assert.Equal("simulado", creada.OrigenSugerencia);
        Assert.NotNull(creada.ConfianzaSugerencia);
        Assert.InRange(creada.ConfianzaSugerencia!.Value, 0d, 1d);

        // Sin unidad indicada, se asigna la responsable de la categoría.
        Assert.Equal("Unidad de Sistemas", creada.UnidadDestino);
    }

    [Fact]
    public async Task LaCategoriaDeclaradaTienePrecedenciaSobreLaSugerida()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var peticion = SolicitudValida with { Categoria = CategoriaSolicitud.Administrativa };

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/solicitudes", peticion);
        var creada = await respuesta.Content.ReadFromJsonAsync<SolicitudRespuesta>();

        Assert.NotNull(creada);
        Assert.Equal("Administrativa", creada!.Categoria);
        Assert.Equal("Tecnologica", creada.CategoriaSugerida);
    }

    [Theory]
    [InlineData("", "Descripción válida y suficientemente larga para pasar.", "Nombre", "correo@upea.bo")]
    [InlineData("Título correcto", "corta", "Nombre", "correo@upea.bo")]
    [InlineData("Título correcto", "Descripción válida y suficientemente larga para pasar.", "", "correo@upea.bo")]
    [InlineData("Título correcto", "Descripción válida y suficientemente larga para pasar.", "Nombre", "no-es-correo")]
    public async Task LosDatosInvalidosSonRechazadosConDetallePorCampo(
        string titulo, string descripcion, string nombre, string correo)
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var peticion = new CrearSolicitud
        {
            Titulo = titulo,
            Descripcion = descripcion,
            SolicitanteNombre = nombre,
            SolicitanteCorreo = correo
        };

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/solicitudes", peticion);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var problema = await respuesta.Content.ReadFromJsonAsync<RespuestaValidacion>();
        Assert.NotNull(problema);
        Assert.NotEmpty(problema!.Errors);
    }

    [Fact]
    public async Task ConsultarUnaSolicitudInexistenteDevuelveNoEncontrado()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var respuesta = await cliente.GetAsync($"/api/v1/solicitudes/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task LaSolicitudAvanzaPorLosEstadosDelFlujo()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        var creada = await CrearAsync(cliente);

        var revision = await cliente.PatchAsJsonAsync(
            $"/api/v1/solicitudes/{creada.Id}/estado",
            new CambiarEstado { NuevoEstado = EstadoSolicitud.EnRevision });

        Assert.Equal(HttpStatusCode.OK, revision.StatusCode);

        var actualizada = await revision.Content.ReadFromJsonAsync<SolicitudRespuesta>();
        Assert.Equal("EnRevision", actualizada!.Estado);
        Assert.Contains("EnProceso", actualizada.TransicionesPermitidas);
    }

    [Fact]
    public async Task UnaTransicionNoPermitidaDevuelveConflicto()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        var creada = await CrearAsync(cliente);

        var respuesta = await cliente.PatchAsJsonAsync(
            $"/api/v1/solicitudes/{creada.Id}/estado",
            new CambiarEstado { NuevoEstado = EstadoSolicitud.Cerrada });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task ElMotivoDelCambioDeEstadoQuedaComoComentarioInterno()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        var creada = await CrearAsync(cliente);

        await cliente.PatchAsJsonAsync(
            $"/api/v1/solicitudes/{creada.Id}/estado",
            new CambiarEstado
            {
                NuevoEstado = EstadoSolicitud.EnRevision,
                Motivo = "Derivado a la unidad correspondiente"
            });

        var publicos = await cliente.GetFromJsonAsync<List<ComentarioRespuesta>>(
            $"/api/v1/solicitudes/{creada.Id}/comentarios");

        var todos = await cliente.GetFromJsonAsync<List<ComentarioRespuesta>>(
            $"/api/v1/solicitudes/{creada.Id}/comentarios?incluirInternos=true");

        Assert.Empty(publicos!);
        Assert.Single(todos!);
        Assert.True(todos![0].EsInterno);
        Assert.Contains("Derivado a la unidad correspondiente", todos[0].Contenido);
    }

    [Fact]
    public async Task ElListadoFiltraPorTextoSinDistinguirMayusculas()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        await cliente.PostAsJsonAsync("/api/v1/solicitudes", SolicitudValida);
        await cliente.PostAsJsonAsync("/api/v1/solicitudes", SolicitudValida with
        {
            Titulo = "Solicitud de certificado de notas",
            Descripcion = "Necesito un certificado de notas para presentarlo en una postulación laboral."
        });

        var pagina = await cliente.GetFromJsonAsync<PaginaRespuesta<SolicitudRespuesta>>(
            "/api/v1/solicitudes?texto=CERTIFICADO");

        Assert.NotNull(pagina);
        Assert.Equal(1, pagina!.Total);
        Assert.Contains("certificado", pagina.Elementos.Single().Titulo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ElListadoOrdenaPorPrioridadDeMayorAMenorUrgencia()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        await cliente.PostAsJsonAsync("/api/v1/solicitudes", SolicitudValida with
        {
            Titulo = "Caso de prioridad baja registrado",
            Prioridad = PrioridadSolicitud.Baja
        });

        await cliente.PostAsJsonAsync("/api/v1/solicitudes", SolicitudValida with
        {
            Titulo = "Caso de prioridad crítica registrado",
            Prioridad = PrioridadSolicitud.Critica
        });

        await cliente.PostAsJsonAsync("/api/v1/solicitudes", SolicitudValida with
        {
            Titulo = "Caso de prioridad media registrado",
            Prioridad = PrioridadSolicitud.Media
        });

        var pagina = await cliente.GetFromJsonAsync<PaginaRespuesta<SolicitudRespuesta>>("/api/v1/solicitudes");

        Assert.NotNull(pagina);
        Assert.Equal(
            new[] { "Critica", "Media", "Baja" },
            pagina!.Elementos.Select(e => e.Prioridad).ToArray());
    }

    [Fact]
    public async Task ElListadoPaginaLosResultados()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        for (var i = 0; i < 5; i++)
        {
            await cliente.PostAsJsonAsync("/api/v1/solicitudes", SolicitudValida with
            {
                Titulo = $"Solicitud número {i} del lote de prueba"
            });
        }

        var pagina = await cliente.GetFromJsonAsync<PaginaRespuesta<SolicitudRespuesta>>(
            "/api/v1/solicitudes?pagina=2&tamanoPagina=2");

        Assert.NotNull(pagina);
        Assert.Equal(5, pagina!.Total);
        Assert.Equal(3, pagina.TotalPaginas);
        Assert.Equal(2, pagina.Elementos.Count);
    }

    [Fact]
    public async Task ElResumenOperativoReflejaLasSolicitudesRegistradas()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();
        await CrearAsync(cliente);

        var resumen = await cliente.GetFromJsonAsync<ResumenOperativo>("/api/v1/estadisticas/resumen");

        Assert.NotNull(resumen);
        Assert.Equal(1, resumen!.Total);
        Assert.Equal(1, resumen.Abiertas);
        Assert.Equal(0, resumen.Cerradas);
        Assert.Contains(resumen.PorEstado, c => c.Clave == "Recibida" && c.Cantidad == 1);
    }

    [Fact]
    public async Task ElServicioInformaSuEstadoYElProveedorActivo()
    {
        var cliente = fabrica.CrearClienteConBaseLimpia();

        var respuesta = await cliente.GetAsync("/salud");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var salud = await respuesta.Content.ReadFromJsonAsync<RespuestaSalud>();
        Assert.Equal("activo", salud!.Estado);
        Assert.Equal("simulado", salud.ProveedorLenguaje);
    }

    private static async Task<SolicitudRespuesta> CrearAsync(HttpClient cliente)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/solicitudes", SolicitudValida);
        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<SolicitudRespuesta>())!;
    }

    private sealed record RespuestaValidacion(Dictionary<string, string[]> Errors);

    private sealed record RespuestaSalud(string Estado, string ProveedorLenguaje);
}
