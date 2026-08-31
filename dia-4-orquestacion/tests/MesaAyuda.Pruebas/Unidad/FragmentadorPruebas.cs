using MesaAyuda.Api.Modulos.Conocimiento;
using MesaAyuda.Api.Nucleo.Configuracion;
using Microsoft.Extensions.Options;

namespace MesaAyuda.Pruebas.Unidad;

public class FragmentadorPruebas
{
    private static Fragmentador Construir(int tamano = 300, int solapamiento = 50) =>
        new(Options.Create(new OpcionesConocimiento
        {
            TamanoFragmento = tamano,
            SolapamientoFragmento = solapamiento
        }));

    [Fact]
    public void UnTextoVacioNoProduceFragmentos()
    {
        Assert.Empty(Construir().Fragmentar(string.Empty));
        Assert.Empty(Construir().Fragmentar("    \n\n   "));
    }

    [Fact]
    public void NingunFragmentoSuperaElTamanoConfigurado()
    {
        var texto = string.Join("\n\n", Enumerable.Range(1, 40)
            .Select(i => $"Este es el párrafo número {i} del documento de prueba y contiene texto suficiente para ocupar espacio."));

        var fragmentos = Construir(tamano: 300).Fragmentar(texto);

        Assert.NotEmpty(fragmentos);
        Assert.All(fragmentos, f => Assert.True(
            f.Contenido.Length <= 300,
            $"El fragmento {f.Orden} tiene {f.Contenido.Length} caracteres."));
    }

    [Fact]
    public void LosFragmentosSeNumeranDeFormaConsecutiva()
    {
        var texto = string.Join("\n\n", Enumerable.Range(1, 20)
            .Select(i => $"Párrafo {i} con contenido suficiente para que el fragmentador tenga material que dividir."));

        var fragmentos = Construir(tamano: 250).Fragmentar(texto);

        Assert.Equal(
            Enumerable.Range(0, fragmentos.Count).ToArray(),
            fragmentos.Select(f => f.Orden).ToArray());
    }

    [Fact]
    public void ReconoceLaReferenciaDeUnArticuloYLaArrastra()
    {
        const string texto = """
            Artículo 7. Plazo general

            El plazo general para resolver una solicitud es de diez días hábiles computados desde su registro en la plataforma institucional correspondiente.

            Se computan únicamente los días hábiles administrativos, excluyendo sábados, domingos y feriados nacionales declarados por la autoridad competente.
            """;

        var fragmentos = Construir(tamano: 200, solapamiento: 30).Fragmentar(texto);

        Assert.NotEmpty(fragmentos);
        Assert.All(fragmentos, f => Assert.Equal("Artículo 7", f.Referencia));
    }

    [Fact]
    public void CadaArticuloConservaSuPropiaReferencia()
    {
        const string texto = """
            Artículo 1. Objeto

            El presente reglamento establece el procedimiento para la atención de solicitudes institucionales.

            Artículo 2. Alcance

            Quedan comprendidos los estudiantes regulares, los egresados y el personal administrativo de la institución.
            """;

        var fragmentos = Construir(tamano: 150, solapamiento: 20).Fragmentar(texto);

        var referencias = fragmentos.Select(f => f.Referencia).Distinct().ToArray();

        Assert.Contains("Artículo 1", referencias);
        Assert.Contains("Artículo 2", referencias);
    }

    [Fact]
    public void ElSolapamientoRepiteElFinalDelFragmentoAnterior()
    {
        var texto = string.Join("\n\n", Enumerable.Range(1, 12)
            .Select(i => $"Bloque {i} con una cantidad de texto pensada para forzar varios cortes sucesivos."));

        var fragmentos = Construir(tamano: 200, solapamiento: 60).Fragmentar(texto);

        Assert.True(fragmentos.Count >= 2, "El texto debía producir más de un fragmento.");

        // Alguna palabra del final del primer fragmento debe reaparecer al
        // comienzo del segundo.
        var finalPrimero = fragmentos[0].Contenido.Split(' ').TakeLast(4);
        var inicioSegundo = fragmentos[1].Contenido;

        Assert.Contains(finalPrimero, palabra => inicioSegundo.Contains(palabra, StringComparison.Ordinal));
    }

    [Fact]
    public void UnParrafoMuyLargoSeParteEnVariosFragmentos()
    {
        var oraciones = string.Join(" ", Enumerable.Range(1, 30)
            .Select(i => $"Esta es la oración número {i} del párrafo extenso."));

        var fragmentos = Construir(tamano: 200).Fragmentar(oraciones);

        Assert.True(fragmentos.Count > 1);
        Assert.All(fragmentos, f => Assert.True(f.Contenido.Length <= 200));
    }

    [Fact]
    public void ElContenidoNoPierdeLasPalabrasDelOriginal()
    {
        const string texto = """
            Artículo 10. Solicitudes urgentes

            Se consideran urgentes las solicitudes vinculadas a plazos de titulación, becas y convocatorias externas debidamente acreditadas ante la unidad responsable.
            """;

        var fragmentos = Construir(tamano: 400).Fragmentar(texto);
        var reconstruido = string.Join(" ", fragmentos.Select(f => f.Contenido));

        foreach (var termino in new[] { "titulación", "becas", "convocatorias", "urgentes" })
        {
            Assert.Contains(termino, reconstruido, StringComparison.OrdinalIgnoreCase);
        }
    }
}
