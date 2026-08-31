using System.Text;
using System.Text.RegularExpressions;
using MesaAyuda.Api.Nucleo.Configuracion;
using Microsoft.Extensions.Options;

namespace MesaAyuda.Api.Modulos.Conocimiento;

/// <summary>
/// Fragmento de texto listo para ser vectorizado.
/// </summary>
/// <param name="Orden">Posición dentro del documento.</param>
/// <param name="Contenido">Texto del fragmento.</param>
/// <param name="Referencia">Ubicación legible, por ejemplo "Artículo 14".</param>
public sealed record TrozoTexto(int Orden, string Contenido, string? Referencia);

/// <summary>
/// Divide un documento en fragmentos del tamaño adecuado para recuperarlos.
///
/// Este paso condiciona toda la calidad del sistema. Fragmentos demasiado
/// grandes diluyen el tema y traen ruido; demasiado pequeños pierden el
/// contexto necesario para entender la respuesta. Por eso el corte se hace
/// respetando la estructura del texto en vez de partirlo cada N caracteres:
/// primero se intenta cortar en párrafos, luego en oraciones y solo como último
/// recurso a mitad de una.
/// </summary>
public sealed partial class Fragmentador(IOptions<OpcionesConocimiento> opciones)
{
    private readonly OpcionesConocimiento _configuracion = opciones.Value;

    /// <summary>
    /// Reconoce los encabezados típicos de un reglamento para poder citar la
    /// ubicación exacta de cada fragmento.
    /// </summary>
    [GeneratedRegex(
        @"^\s*(?<referencia>(?:art[íi]culo|cap[íi]tulo|t[íi]tulo|secci[óo]n|anexo|disposici[óo]n)\s+[^\r\n:.]{1,40})",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex EncabezadoLegal();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex EspaciosRepetidos();

    [GeneratedRegex(@"(?<=[.!?:])\s+")]
    private static partial Regex FinDeOracion();

    /// <summary>
    /// Divide el texto en fragmentos, arrastrando la última referencia
    /// encontrada para que todo fragmento sepa de qué parte del documento viene.
    /// </summary>
    public IReadOnlyList<TrozoTexto> Fragmentar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return [];
        }

        var parrafos = SepararEnParrafos(texto);
        var trozos = new List<TrozoTexto>();

        var acumulado = new StringBuilder();
        string? referenciaActual = null;
        string? referenciaDelTrozo = null;

        foreach (var parrafo in parrafos)
        {
            var referenciaDetectada = DetectarReferencia(parrafo);
            if (referenciaDetectada is not null)
            {
                referenciaActual = referenciaDetectada;
            }

            referenciaDelTrozo ??= referenciaActual;

            // Un párrafo que por sí solo supera el tamaño objetivo se parte en
            // oraciones; de lo contrario quedaría un fragmento desproporcionado.
            if (parrafo.Length > _configuracion.TamanoFragmento)
            {
                VolcarAcumulado(trozos, acumulado, ref referenciaDelTrozo, referenciaActual);

                foreach (var pedazo in PartirPorOraciones(parrafo))
                {
                    trozos.Add(new TrozoTexto(trozos.Count, pedazo, referenciaActual));
                }

                referenciaDelTrozo = null;
                continue;
            }

            if (acumulado.Length + parrafo.Length + 2 > _configuracion.TamanoFragmento && acumulado.Length > 0)
            {
                var cerrado = acumulado.ToString().Trim();
                trozos.Add(new TrozoTexto(trozos.Count, cerrado, referenciaDelTrozo));

                // El nuevo fragmento arranca repitiendo el final del anterior,
                // para que una idea cortada al medio siga siendo recuperable.
                acumulado.Clear();
                acumulado.Append(TomarCola(cerrado, _configuracion.SolapamientoFragmento));
                referenciaDelTrozo = referenciaActual;
            }

            if (acumulado.Length > 0)
            {
                acumulado.Append("\n\n");
            }

            acumulado.Append(parrafo);
        }

        VolcarAcumulado(trozos, acumulado, ref referenciaDelTrozo, referenciaActual);

        return trozos;
    }

    private void VolcarAcumulado(
        List<TrozoTexto> trozos,
        StringBuilder acumulado,
        ref string? referenciaDelTrozo,
        string? referenciaActual)
    {
        var contenido = acumulado.ToString().Trim();

        // Un residuo de pocas palabras no aporta nada como fragmento propio.
        if (contenido.Length >= 40)
        {
            trozos.Add(new TrozoTexto(trozos.Count, contenido, referenciaDelTrozo));
        }

        acumulado.Clear();
        referenciaDelTrozo = referenciaActual;
    }

    private IEnumerable<string> PartirPorOraciones(string parrafo)
    {
        var oraciones = FinDeOracion().Split(parrafo);
        var acumulado = new StringBuilder();

        foreach (var oracion in oraciones)
        {
            if (acumulado.Length + oracion.Length > _configuracion.TamanoFragmento && acumulado.Length > 0)
            {
                var cerrado = acumulado.ToString().Trim();
                yield return cerrado;

                acumulado.Clear();
                acumulado.Append(TomarCola(cerrado, _configuracion.SolapamientoFragmento));
            }

            if (acumulado.Length > 0)
            {
                acumulado.Append(' ');
            }

            acumulado.Append(oracion.Trim());

            // Una sola oración más larga que el tamaño objetivo se corta seca:
            // no queda otra forma de acotarla.
            while (acumulado.Length > _configuracion.TamanoFragmento)
            {
                yield return acumulado.ToString(0, _configuracion.TamanoFragmento).Trim();
                acumulado.Remove(0, _configuracion.TamanoFragmento);
            }
        }

        var resto = acumulado.ToString().Trim();
        if (resto.Length >= 40)
        {
            yield return resto;
        }
    }

    private static IReadOnlyList<string> SepararEnParrafos(string texto)
    {
        var normalizado = texto.Replace("\r\n", "\n").Replace('\r', '\n');

        return normalizado
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => EspaciosRepetidos().Replace(p.Replace('\n', ' '), " ").Trim())
            .Where(p => p.Length > 0)
            .ToArray();
    }

    private static string? DetectarReferencia(string parrafo)
    {
        var coincidencia = EncabezadoLegal().Match(parrafo);

        if (!coincidencia.Success)
        {
            return null;
        }

        var referencia = coincidencia.Groups["referencia"].Value.Trim();

        // Se normaliza la mayúscula inicial para que las citas se vean parejas.
        return char.ToUpperInvariant(referencia[0]) + referencia[1..];
    }

    /// <summary>
    /// Devuelve el final del texto, recortado al comienzo de una palabra para
    /// que el solapamiento no empiece a mitad de un término.
    /// </summary>
    private static string TomarCola(string texto, int longitud)
    {
        if (longitud <= 0 || texto.Length <= longitud)
        {
            return texto;
        }

        var cola = texto[^longitud..];
        var espacio = cola.IndexOf(' ');

        return espacio > 0 ? cola[(espacio + 1)..] : cola;
    }
}
