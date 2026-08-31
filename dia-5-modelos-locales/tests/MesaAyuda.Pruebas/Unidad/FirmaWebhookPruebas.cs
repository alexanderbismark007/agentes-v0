using MesaAyuda.Api.Modulos.Orquestacion;

namespace MesaAyuda.Pruebas.Unidad;

public class FirmaWebhookPruebas
{
    private const string Secreto = "secreto-de-prueba";
    private const string Cuerpo = """{"titulo":"Solicitud de prueba","descripcion":"Contenido"}""";

    [Fact]
    public void LaFirmaDelMismoCuerpoEsSiempreLaMisma()
    {
        Assert.Equal(
            FirmaWebhook.Calcular(Cuerpo, Secreto),
            FirmaWebhook.Calcular(Cuerpo, Secreto));
    }

    [Fact]
    public void UnaFirmaValidaSeAcepta()
    {
        var firma = FirmaWebhook.Calcular(Cuerpo, Secreto);

        Assert.True(FirmaWebhook.EsValida(Cuerpo, Secreto, firma));
    }

    [Fact]
    public void SeAceptaElPrefijoConvencional()
    {
        var firma = FirmaWebhook.Calcular(Cuerpo, Secreto);

        Assert.True(FirmaWebhook.EsValida(Cuerpo, Secreto, $"sha256={firma}"));
        Assert.True(FirmaWebhook.EsValida(Cuerpo, Secreto, $"SHA256={firma.ToUpperInvariant()}"));
    }

    [Fact]
    public void CambiarUnSoloCaracterDelCuerpoInvalidaLaFirma()
    {
        var firma = FirmaWebhook.Calcular(Cuerpo, Secreto);
        var alterado = Cuerpo.Replace("prueba", "pruebA");

        Assert.False(FirmaWebhook.EsValida(alterado, Secreto, firma));
    }

    [Fact]
    public void UnSecretoDistintoNoValidaLaFirma()
    {
        var firma = FirmaWebhook.Calcular(Cuerpo, Secreto);

        Assert.False(FirmaWebhook.EsValida(Cuerpo, "otro-secreto", firma));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("firma-inventada")]
    public void UnaFirmaAusenteOInventadaSeRechaza(string? firma)
    {
        Assert.False(FirmaWebhook.EsValida(Cuerpo, Secreto, firma));
    }

    [Fact]
    public void LaHuellaDistingueContenidosDistintos()
    {
        var primera = FirmaWebhook.Huella(Cuerpo);
        var segunda = FirmaWebhook.Huella(Cuerpo + " ");

        Assert.NotEqual(primera, segunda);
        Assert.Equal(64, primera.Length);
    }

    [Fact]
    public void LaHuellaDelMismoContenidoEsEstable()
    {
        Assert.Equal(FirmaWebhook.Huella(Cuerpo), FirmaWebhook.Huella(Cuerpo));
    }
}
