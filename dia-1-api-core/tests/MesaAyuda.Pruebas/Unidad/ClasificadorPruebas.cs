using MesaAyuda.Api.Dominio.Solicitudes;
using MesaAyuda.Api.Modulos.Solicitudes;
using MesaAyuda.Api.Nucleo.Proveedores;

namespace MesaAyuda.Pruebas.Unidad;

public class ProveedorSimuladoPruebas
{
    private readonly ProveedorSimulado _proveedor = new();

    [Theory]
    [InlineData("No puedo acceder al sistema, olvidé mi contraseña de usuario", CategoriaSolicitud.Tecnologica)]
    [InlineData("Necesito un certificado y una constancia para el trámite", CategoriaSolicitud.Administrativa)]
    [InlineData("Consulta sobre el pago del arancel y la cuota de matrícula", CategoriaSolicitud.Financiera)]
    [InlineData("El aula no tiene iluminación y el baño está sin agua", CategoriaSolicitud.Infraestructura)]
    [InlineData("Revisión de la nota del examen de la materia con el docente", CategoriaSolicitud.Academica)]
    public async Task ReconoceLaCategoriaSegunLasSenalesDelTexto(string texto, CategoriaSolicitud esperada)
    {
        var resultado = await ClasificarAsync(texto);

        Assert.Equal(esperada, resultado.Categoria);
        Assert.True(resultado.Confianza > 0d);
    }

    [Fact]
    public async Task DevuelveOtraCuandoNoEncuentraSenalesConocidas()
    {
        var resultado = await ClasificarAsync("Buenos días, deseo información general por favor.");

        Assert.Equal(CategoriaSolicitud.Otra, resultado.Categoria);
        Assert.True(resultado.Confianza < 0.5d);
    }

    [Fact]
    public async Task LaMismaEntradaProduceSiempreElMismoResultado()
    {
        const string texto = "El servidor de la plataforma no responde y el correo institucional falla";

        var primera = await ClasificarAsync(texto);
        var segunda = await ClasificarAsync(texto);

        Assert.Equal(primera.Categoria, segunda.Categoria);
        Assert.Equal(primera.Confianza, segunda.Confianza);
    }

    [Fact]
    public async Task LaConfianzaSiempreQuedaEntreCeroYUno()
    {
        string[] textos =
        [
            "sistema plataforma correo usuario acceso servidor red internet wifi contraseña",
            "texto sin ninguna señal reconocible",
            "pago beca aula nota certificado"
        ];

        foreach (var texto in textos)
        {
            var resultado = await ClasificarAsync(texto);
            Assert.InRange(resultado.Confianza, 0d, 1d);
        }
    }

    private async Task<ResultadoClasificacion> ClasificarAsync(string texto)
    {
        var peticion = new PeticionLenguaje
        {
            Mensajes = [MensajeLenguaje.Usuario(texto)],
            RespuestaJson = true
        };

        var respuesta = await _proveedor.CompletarAsync(peticion);
        return ClasificadorSolicitudes.Interpretar(respuesta.Contenido, _proveedor.Nombre);
    }
}

public class InterpretacionRespuestaPruebas
{
    [Fact]
    public void InterpretaUnJsonBienFormado()
    {
        const string contenido = """{"categoria":"Financiera","confianza":0.87,"justificacion":"Menciona un pago."}""";

        var resultado = ClasificadorSolicitudes.Interpretar(contenido, "prueba");

        Assert.Equal(CategoriaSolicitud.Financiera, resultado.Categoria);
        Assert.Equal(0.87d, resultado.Confianza);
        Assert.Equal("Menciona un pago.", resultado.Justificacion);
        Assert.Equal("prueba", resultado.Origen);
    }

    [Fact]
    public void RescataElJsonAunqueVengaEnvueltoEnTexto()
    {
        const string contenido = """
            Claro, aquí tienes el resultado:
            ```json
            {"categoria":"Tecnologica","confianza":0.6,"justificacion":"Problema de acceso."}
            ```
            """;

        var resultado = ClasificadorSolicitudes.Interpretar(contenido, "prueba");

        Assert.Equal(CategoriaSolicitud.Tecnologica, resultado.Categoria);
        Assert.Equal(0.6d, resultado.Confianza);
    }

    [Theory]
    [InlineData("")]
    [InlineData("No fue posible clasificar la solicitud.")]
    [InlineData("{ esto no es json válido")]
    public void AnteUnaRespuestaInutilizableNoFallaYDevuelveOtra(string contenido)
    {
        var resultado = ClasificadorSolicitudes.Interpretar(contenido, "prueba");

        Assert.Equal(CategoriaSolicitud.Otra, resultado.Categoria);
        Assert.Equal(0d, resultado.Confianza);
    }

    [Fact]
    public void UnaCategoriaDesconocidaSeTratacomoOtra()
    {
        const string contenido = """{"categoria":"Deportiva","confianza":0.9,"justificacion":"Inventada."}""";

        var resultado = ClasificadorSolicitudes.Interpretar(contenido, "prueba");

        Assert.Equal(CategoriaSolicitud.Otra, resultado.Categoria);
    }

    [Fact]
    public void UnaConfianzaFueraDeRangoSeAjustaAlLimite()
    {
        const string contenido = """{"categoria":"Academica","confianza":4.5,"justificacion":"Exagerada."}""";

        var resultado = ClasificadorSolicitudes.Interpretar(contenido, "prueba");

        Assert.Equal(1d, resultado.Confianza);
    }
}
