using System.Text;
using MesaAyuda.Api.Modulos.Agente.Contratos;
using MesaAyuda.Api.Modulos.Solicitudes;
using MesaAyuda.Api.Nucleo.Proveedores;

namespace MesaAyuda.Api.Modulos.Agente;

/// <summary>
/// Genera el reporte ejecutivo de la mesa de ayuda.
///
/// A diferencia del agente, aquí no hay elección de herramientas: los datos que
/// alimentan el reporte están fijados de antemano. Es una decisión deliberada.
/// Un reporte que la dirección va a leer no debe variar según lo que el modelo
/// decida consultar ese día: los números se obtienen siempre igual, y el modelo
/// solo se encarga de redactar la lectura de esos números.
///
/// Además, los indicadores se calculan y se muestran aparte del texto redactado,
/// de modo que cualquier afirmación del reporte pueda contrastarse contra el
/// dato duro que la originó.
/// </summary>
public sealed class ServicioReporte(
    ServicioEstadisticas estadisticas,
    ServicioSolicitudes solicitudes,
    IProveedorLenguaje proveedor,
    ILogger<ServicioReporte> registro)
{
    private const string Instruccion =
        "Eres analista de una mesa de ayuda universitaria. Redactas un reporte ejecutivo breve " +
        "para la dirección, en español.\n\n" +
        "Reglas:\n" +
        "1. Usa exclusivamente los datos entregados. No inventes cifras ni tendencias.\n" +
        "2. Señala los puntos que requieren atención, en particular los casos rezagados y los críticos.\n" +
        "3. Cierra con dos o tres recomendaciones concretas y accionables.\n" +
        "4. No superes las 250 palabras.";

    public async Task<ReporteEjecutivo> GenerarAsync(CancellationToken cancelacion = default)
    {
        var resumen = await estadisticas.ResumirAsync(cancelacion);

        var criticas = await solicitudes.ListarAsync(
            new Modulos.Solicitudes.Contratos.FiltroSolicitudes
            {
                Prioridad = Dominio.Solicitudes.PrioridadSolicitud.Critica,
                TamanoPagina = 10
            },
            cancelacion);

        var datos = ConstruirDatos(resumen, criticas.Total);

        var respuesta = await proveedor.CompletarAsync(
            new PeticionLenguaje
            {
                Mensajes =
                [
                    MensajeLenguaje.Sistema(Instruccion),
                    MensajeLenguaje.Usuario($"Datos del período:\n\n{datos}")
                ],
                Temperatura = 0.2d,
                MaximoTokens = 600
            },
            cancelacion);

        registro.LogInformation(
            "Reporte ejecutivo generado sobre {Total} solicitudes con el proveedor {Proveedor}.",
            resumen.Total, proveedor.Nombre);

        return new ReporteEjecutivo
        {
            Titulo = $"Reporte ejecutivo de la mesa de ayuda al {DateTimeOffset.UtcNow:yyyy-MM-dd}",
            Contenido = respuesta.Contenido,
            DatosConsultados =
            [
                new PasoEjecutado
                {
                    Numero = 1,
                    Herramienta = "obtener_estadisticas",
                    Argumentos = "{}",
                    Exitosa = true,
                    Resultado = datos,
                    Milisegundos = 0
                },
                new PasoEjecutado
                {
                    Numero = 2,
                    Herramienta = "buscar_solicitudes",
                    Argumentos = "{\"prioridad\":\"Critica\"}",
                    Exitosa = true,
                    Resultado = $"{criticas.Total} solicitudes de prioridad crítica",
                    Milisegundos = 0
                }
            ],
            GeneradoEn = DateTimeOffset.UtcNow,
            ProveedorLenguaje = proveedor.Nombre
        };
    }

    private static string ConstruirDatos(ResumenOperativo resumen, int criticas)
    {
        var constructor = new StringBuilder();

        constructor.Append("- Total de solicitudes: ").AppendLine(resumen.Total.ToString());
        constructor.Append("- Abiertas: ").AppendLine(resumen.Abiertas.ToString());
        constructor.Append("- Cerradas o rechazadas: ").AppendLine(resumen.Cerradas.ToString());
        constructor.Append("- Rezagadas (abiertas con más de tres días): ").AppendLine(resumen.Rezagadas.ToString());
        constructor.Append("- De prioridad crítica: ").AppendLine(criticas.ToString());
        constructor.Append("- Tiempo promedio de resolución: ")
            .Append(resumen.HorasPromedioResolucion).AppendLine(" horas");

        constructor.Append("- Distribución por estado: ")
            .AppendLine(string.Join(", ", resumen.PorEstado.Select(c => $"{c.Clave} {c.Cantidad}")));
        constructor.Append("- Distribución por categoría: ")
            .AppendLine(string.Join(", ", resumen.PorCategoria.Select(c => $"{c.Clave} {c.Cantidad}")));
        constructor.Append("- Distribución por prioridad: ")
            .AppendLine(string.Join(", ", resumen.PorPrioridad.Select(c => $"{c.Clave} {c.Cantidad}")));

        return constructor.ToString().TrimEnd();
    }
}
