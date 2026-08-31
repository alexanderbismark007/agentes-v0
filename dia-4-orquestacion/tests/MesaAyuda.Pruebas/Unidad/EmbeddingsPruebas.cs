using MesaAyuda.Api.Nucleo.Configuracion;
using MesaAyuda.Api.Nucleo.Proveedores;

namespace MesaAyuda.Pruebas.Unidad;

public class EmbeddingsPruebas
{
    private readonly ProveedorEmbeddingsSimulado _proveedor = new();

    [Fact]
    public async Task ElVectorTieneLaDimensionEstandarDelSistema()
    {
        var vector = await _proveedor.GenerarAsync("El plazo general es de diez días hábiles.");

        Assert.Equal(Dimensiones.Estandar, vector.Length);
        Assert.Equal(Dimensiones.Estandar, _proveedor.Dimensiones);
    }

    [Fact]
    public async Task ElMismoTextoProduceSiempreElMismoVector()
    {
        const string texto = "Requisitos para solicitar el certificado de notas.";

        var primero = await _proveedor.GenerarAsync(texto);
        var segundo = await _proveedor.GenerarAsync(texto);

        Assert.Equal(primero, segundo);
    }

    [Fact]
    public async Task ElVectorQuedaNormalizado()
    {
        var vector = await _proveedor.GenerarAsync("Plazos de atención de solicitudes urgentes.");

        var longitud = Math.Sqrt(vector.Sum(v => (double)v * v));

        Assert.Equal(1d, longitud, precision: 4);
    }

    [Fact]
    public async Task DosTextosSobreElMismoTemaSeParecenMasQueDosTextosDistintos()
    {
        var plazos = await _proveedor.GenerarAsync(
            "El plazo general para resolver una solicitud es de diez días hábiles.");

        var plazosSimilar = await _proveedor.GenerarAsync(
            "El plazo para resolver la solicitud alcanza diez días hábiles como máximo.");

        var infraestructura = await _proveedor.GenerarAsync(
            "Las luminarias del aula doscientos cuatro se encuentran quemadas.");

        var similitudCercana = Vectores.SimilitudCoseno(plazos, plazosSimilar);
        var similitudLejana = Vectores.SimilitudCoseno(plazos, infraestructura);

        Assert.True(
            similitudCercana > similitudLejana,
            $"Se esperaba mayor cercanía entre textos del mismo tema ({similitudCercana:F3} contra {similitudLejana:F3}).");
    }

    [Fact]
    public async Task IgnoraAcentosYMayusculasAlComparar()
    {
        var conAcentos = await _proveedor.GenerarAsync("Solicitud de titulación académica");
        var sinAcentos = await _proveedor.GenerarAsync("SOLICITUD DE TITULACION ACADEMICA");

        Assert.Equal(1d, Vectores.SimilitudCoseno(conAcentos, sinAcentos), precision: 4);
    }

    [Fact]
    public async Task ElLoteDevuelveUnVectorPorTextoEnElMismoOrden()
    {
        string[] textos = ["primer texto de prueba", "segundo texto distinto", "tercer texto diferente"];

        var lote = await _proveedor.GenerarLoteAsync(textos);

        Assert.Equal(3, lote.Count);

        for (var i = 0; i < textos.Length; i++)
        {
            Assert.Equal(await _proveedor.GenerarAsync(textos[i]), lote[i]);
        }
    }

    [Fact]
    public async Task UnTextoSinTerminosUtilesProduceUnVectorNulo()
    {
        var vector = await _proveedor.GenerarAsync("de la en y o");

        Assert.All(vector, componente => Assert.Equal(0f, componente));
    }

    [Fact]
    public void LaSimilitudDeUnVectorConsigoMismoEsUno()
    {
        float[] vector = [0.5f, -0.5f, 0.5f, -0.5f];

        Assert.Equal(1d, Vectores.SimilitudCoseno(vector, vector), precision: 6);
    }

    [Fact]
    public void CompararVectoresDeDistintaDimensionEsUnError()
    {
        Assert.Throws<ArgumentException>(() =>
            Vectores.SimilitudCoseno([1f, 0f], [1f, 0f, 0f]));
    }

    [Fact]
    public void UnVectorNuloNoTieneSimilitudConNadie()
    {
        Assert.Equal(0d, Vectores.SimilitudCoseno([0f, 0f, 0f], [1f, 2f, 3f]));
    }
}
