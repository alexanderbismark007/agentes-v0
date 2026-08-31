using System.Text.Json;
using MesaAyuda.Api.Modulos.Agente;

namespace MesaAyuda.Pruebas.Unidad;

public class DecisionPruebas
{
    [Fact]
    public void InterpretaLaInvocacionDeUnaHerramienta()
    {
        const string contenido =
            """{"accion":"herramienta","herramienta":"buscar_solicitudes","argumentos":{"estado":"EnProceso","cantidad":5}}""";

        var decision = Decision.Interpretar(contenido);

        Assert.False(decision.EsRespuestaFinal);
        Assert.Equal("buscar_solicitudes", decision.Herramienta);
        Assert.Equal("EnProceso", decision.Argumentos.GetProperty("estado").GetString());
        Assert.Equal(5, decision.Argumentos.GetProperty("cantidad").GetInt32());
    }

    [Fact]
    public void InterpretaLaRespuestaFinal()
    {
        const string contenido = """{"accion":"responder","respuesta":"Hay tres solicitudes en proceso."}""";

        var decision = Decision.Interpretar(contenido);

        Assert.True(decision.EsRespuestaFinal);
        Assert.Equal("Hay tres solicitudes en proceso.", decision.Respuesta);
    }

    [Fact]
    public void RescataElJsonAunqueVengaEnvueltoEnTexto()
    {
        const string contenido = """
            Voy a consultar el reglamento:
            ```json
            {"accion":"herramienta","herramienta":"consultar_reglamento","argumentos":{"pregunta":"¿Cuál es el plazo?"}}
            ```
            """;

        var decision = Decision.Interpretar(contenido);

        Assert.False(decision.EsRespuestaFinal);
        Assert.Equal("consultar_reglamento", decision.Herramienta);
    }

    [Fact]
    public void UnaHerramientaSinArgumentosRecibeUnObjetoVacio()
    {
        const string contenido = """{"accion":"herramienta","herramienta":"obtener_estadisticas"}""";

        var decision = Decision.Interpretar(contenido);

        Assert.False(decision.EsRespuestaFinal);
        Assert.Equal(JsonValueKind.Object, decision.Argumentos.ValueKind);
        Assert.False(decision.Argumentos.EnumerateObject().Any());
    }

    [Fact]
    public void UnaInvocacionSinNombreDeHerramientaNoDetieneElAgente()
    {
        const string contenido = """{"accion":"herramienta","argumentos":{}}""";

        var decision = Decision.Interpretar(contenido);

        Assert.True(decision.EsRespuestaFinal);
        Assert.Contains("no especificó cuál", decision.Respuesta);
    }

    [Theory]
    [InlineData("El sistema no pudo procesar la consulta.")]
    [InlineData("{ esto no es json")]
    [InlineData("")]
    public void UnaSalidaSinFormatoSeTomaComoRespuestaFinal(string contenido)
    {
        var decision = Decision.Interpretar(contenido);

        Assert.True(decision.EsRespuestaFinal);
    }

    [Fact]
    public void LosArgumentosSobrevivenAlCierreDelDocumentoJson()
    {
        const string contenido =
            """{"accion":"herramienta","herramienta":"ver_solicitud","argumentos":{"codigo":"SOL-2026-000003"}}""";

        var decision = Decision.Interpretar(contenido);

        // El elemento se clona al interpretarlo; si no fuera así, leerlo
        // después de cerrar el documento provocaría una excepción.
        GC.Collect();

        Assert.Equal("SOL-2026-000003", decision.Argumentos.GetProperty("codigo").GetString());
    }
}

/// <summary>
/// Casos observados con modelos pequeños que corren de forma local. Estos
/// modelos siguen el formato pedido con menos rigor que un servicio grande, y
/// el intérprete debe absorber esas variantes: interpretarlas mal significa
/// mostrarle al usuario la mecánica interna del agente.
/// </summary>
public class DecisionModelosLocalesPruebas
{
    [Fact]
    public void ElNombreDeLaHerramientaEnElCampoAccionSeInterpretaIgual()
    {
        // Salida real de un modelo local de 1.5B de parámetros.
        const string contenido = """{"accion":"obtener_estadisticas","herramienta":"obtener_estadisticas"}""";

        var decision = Decision.Interpretar(contenido);

        Assert.False(decision.EsRespuestaFinal);
        Assert.Equal("obtener_estadisticas", decision.Herramienta);
    }

    [Fact]
    public void UnaAccionMalRotuladaSeTomaComoNombreDeHerramienta()
    {
        const string contenido = """{"accion":"buscar_solicitudes","argumentos":{"estado":"EnProceso"}}""";

        var decision = Decision.Interpretar(contenido);

        Assert.False(decision.EsRespuestaFinal);
        Assert.Equal("buscar_solicitudes", decision.Herramienta);
        Assert.Equal("EnProceso", decision.Argumentos.GetProperty("estado").GetString());
    }

    [Fact]
    public void SeAceptanLosNombresEnInglesDeLosArgumentos()
    {
        const string contenido = """{"herramienta":"ver_solicitud","arguments":{"codigo":"SOL-2026-000004"}}""";

        var decision = Decision.Interpretar(contenido);

        Assert.Equal("SOL-2026-000004", decision.Argumentos.GetProperty("codigo").GetString());
    }

    [Fact]
    public void UnaRespuestaRedactadaTienePrecedenciaSobreElRotulo()
    {
        const string contenido = """{"accion":"herramienta","respuesta":"Hay ocho solicitudes registradas."}""";

        var decision = Decision.Interpretar(contenido);

        Assert.True(decision.EsRespuestaFinal);
        Assert.Equal("Hay ocho solicitudes registradas.", decision.Respuesta);
    }

    [Fact]
    public void UnJsonIninteligibleNoSeMuestraAlUsuario()
    {
        const string contenido = """{"paso":1,"pensamiento":"debo revisar los datos"}""";

        var decision = Decision.Interpretar(contenido);

        Assert.True(decision.EsRespuestaFinal);
        Assert.DoesNotContain("pensamiento", decision.Respuesta);
        Assert.DoesNotContain("{", decision.Respuesta);
    }
}
