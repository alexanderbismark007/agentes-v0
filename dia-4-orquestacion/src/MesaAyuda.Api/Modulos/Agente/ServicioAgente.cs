using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MesaAyuda.Api.Modulos.Agente.Contratos;
using MesaAyuda.Api.Modulos.Agente.Herramientas;
using MesaAyuda.Api.Nucleo.Configuracion;
using MesaAyuda.Api.Nucleo.Proveedores;
using Microsoft.Extensions.Options;

namespace MesaAyuda.Api.Modulos.Agente;

/// <summary>
/// Ejecuta el ciclo de razonamiento del agente.
///
/// El ciclo es siempre el mismo: se le presenta al modelo la pregunta y el
/// catálogo de herramientas; el modelo responde eligiendo una herramienta o
/// dando la respuesta final; si eligió una herramienta, se ejecuta y su
/// resultado se incorpora a la conversación; y se repite.
///
/// Tres límites acotan ese ciclo, porque un agente sin límites es un riesgo
/// operativo y no una funcionalidad:
///
/// - Un máximo de iteraciones, para que no razone indefinidamente.
/// - Un conjunto cerrado de herramientas, para que no haga nada no previsto.
/// - Aprobación humana obligatoria para todo lo que modifique datos.
/// </summary>
public sealed class ServicioAgente(
    IEnumerable<IHerramientaAgente> herramientas,
    IProveedorLenguaje proveedor,
    IOptions<OpcionesAgente> opciones,
    ILogger<ServicioAgente> registro)
{
    private readonly OpcionesAgente _configuracion = opciones.Value;
    private readonly IReadOnlyList<IHerramientaAgente> _herramientas = herramientas.ToArray();

    public IReadOnlyCollection<HerramientaDisponible> Catalogo() =>
        _herramientas
            .Select(h => new HerramientaDisponible
            {
                Nombre = h.Nombre,
                Descripcion = h.Descripcion,
                Riesgo = h.Riesgo.ToString(),
                RequiereAprobacion = h.Riesgo == NivelRiesgo.Escritura
            })
            .ToArray();

    public async Task<RespuestaAgente> ResponderAsync(
        ConsultaAgente consulta,
        CancellationToken cancelacion = default)
    {
        var cronometro = Stopwatch.StartNew();

        var conversacion = new List<MensajeLenguaje>
        {
            MensajeLenguaje.Sistema(ConstruirInstruccion(consulta.AutorizarEscritura)),
            MensajeLenguaje.Usuario(consulta.Pregunta)
        };

        var pasos = new List<PasoEjecutado>();
        var pendientes = new List<AccionPendiente>();

        var tokensEntrada = 0;
        var tokensSalida = 0;

        for (var iteracion = 1; iteracion <= _configuracion.MaximoIteraciones; iteracion++)
        {
            var respuesta = await proveedor.CompletarAsync(
                new PeticionLenguaje
                {
                    Mensajes = conversacion,
                    Temperatura = 0d,
                    MaximoTokens = 800,
                    RespuestaJson = true
                },
                cancelacion);

            tokensEntrada += respuesta.TokensEntrada;
            tokensSalida += respuesta.TokensSalida;

            var decision = Decision.Interpretar(respuesta.Contenido);

            // El modelo decidió que ya tiene lo necesario para responder.
            if (decision.EsRespuestaFinal)
            {
                return Construir(consulta, decision.Respuesta!, pasos, pendientes,
                    alcanzoLimite: false, tokensEntrada, tokensSalida, cronometro);
            }

            var herramienta = _herramientas.FirstOrDefault(h =>
                string.Equals(h.Nombre, decision.Herramienta, StringComparison.OrdinalIgnoreCase));

            // El modelo pidió algo que no existe. En lugar de fallar, se le
            // informa el error para que corrija en la siguiente iteración.
            if (herramienta is null)
            {
                registro.LogWarning("El agente solicitó una herramienta inexistente: {Herramienta}", decision.Herramienta);

                conversacion.Add(MensajeLenguaje.Asistente(respuesta.Contenido));
                conversacion.Add(MensajeLenguaje.Usuario(
                    $"La herramienta '{decision.Herramienta}' no existe. " +
                    $"Herramientas disponibles: {string.Join(", ", _herramientas.Select(h => h.Nombre))}."));

                continue;
            }

            // Frontera de seguridad: una herramienta que modifica datos no se
            // ejecuta sin autorización explícita, por más que el modelo insista.
            if (herramienta.Riesgo == NivelRiesgo.Escritura && !consulta.AutorizarEscritura)
            {
                registro.LogInformation(
                    "Acción {Herramienta} retenida por falta de autorización humana.", herramienta.Nombre);

                pendientes.Add(new AccionPendiente
                {
                    Herramienta = herramienta.Nombre,
                    Argumentos = decision.Argumentos.GetRawText(),
                    Motivo = "La herramienta modifica datos y requiere aprobación de un operador."
                });

                conversacion.Add(MensajeLenguaje.Asistente(respuesta.Contenido));
                conversacion.Add(MensajeLenguaje.Usuario(
                    $"La acción '{herramienta.Nombre}' quedó pendiente de aprobación humana y no se ejecutó. " +
                    "Informa al usuario que la acción requiere autorización y responde con lo que ya sabes."));

                continue;
            }

            var cronometroPaso = Stopwatch.StartNew();
            var resultado = await EjecutarAsync(herramienta, decision.Argumentos, cancelacion);
            cronometroPaso.Stop();

            pasos.Add(new PasoEjecutado
            {
                Numero = pasos.Count + 1,
                Herramienta = herramienta.Nombre,
                Argumentos = decision.Argumentos.GetRawText(),
                Exitosa = resultado.Exitosa,
                Resultado = resultado.Detalle,
                Milisegundos = (int)cronometroPaso.ElapsedMilliseconds
            });

            registro.LogInformation(
                "Paso {Numero}: {Herramienta} en {Milisegundos} ms ({Estado})",
                pasos.Count, herramienta.Nombre, cronometroPaso.ElapsedMilliseconds,
                resultado.Exitosa ? "correcta" : "fallida");

            conversacion.Add(MensajeLenguaje.Asistente(respuesta.Contenido));
            conversacion.Add(MensajeLenguaje.Usuario(
                $"Resultado de {herramienta.Nombre}:\n{resultado.Contenido}"));
        }

        // Se agotaron las iteraciones sin que el modelo cerrara la respuesta.
        registro.LogWarning(
            "El agente alcanzó el límite de {Maximo} iteraciones sin concluir.",
            _configuracion.MaximoIteraciones);

        return Construir(
            consulta,
            "No fue posible completar la consulta dentro del límite de pasos permitido. " +
            "Los datos recopilados hasta ahora figuran en la traza de ejecución.",
            pasos, pendientes, alcanzoLimite: true, tokensEntrada, tokensSalida, cronometro);
    }

    private async Task<ResultadoHerramienta> EjecutarAsync(
        IHerramientaAgente herramienta,
        JsonElement argumentos,
        CancellationToken cancelacion)
    {
        try
        {
            // Cada herramienta tiene su propio tiempo máximo: una que se cuelgue
            // no debe bloquear todo el razonamiento.
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
            limite.CancelAfter(TimeSpan.FromSeconds(_configuracion.TiempoMaximoHerramientaSegundos));

            return await herramienta.EjecutarAsync(argumentos, limite.Token);
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            return ResultadoHerramienta.Fallida(
                $"superó el tiempo máximo de {_configuracion.TiempoMaximoHerramientaSegundos} segundos.");
        }
        catch (Exception excepcion)
        {
            // El fallo de una herramienta se le informa al modelo como una
            // observación más, para que pueda intentar otro camino.
            registro.LogError(excepcion, "Falló la herramienta {Herramienta}.", herramienta.Nombre);
            return ResultadoHerramienta.Fallida(excepcion.Message);
        }
    }

    private string ConstruirInstruccion(bool autorizaEscritura)
    {
        var constructor = new StringBuilder();

        constructor.AppendLine(
            "Eres el asistente operativo de la mesa de ayuda de una universidad pública. " +
            "Respondes consultas del personal apoyándote en las herramientas disponibles.");
        constructor.AppendLine();
        constructor.AppendLine("Herramientas disponibles:");

        foreach (var herramienta in _herramientas)
        {
            constructor
                .Append("- ").Append(herramienta.Nombre).Append(": ").AppendLine(herramienta.Descripcion);
            constructor
                .Append("  Parámetros: ")
                .AppendLine(JsonSerializer.Serialize(herramienta.EsquemaParametros, OpcionesJson.Predeterminadas));

            if (herramienta.Riesgo == NivelRiesgo.Escritura)
            {
                constructor.AppendLine(autorizaEscritura
                    ? "  Modifica datos. El operador ya autorizó su uso en esta consulta."
                    : "  Modifica datos y NO está autorizada en esta consulta. No la invoques.");
            }
        }

        constructor.AppendLine();
        constructor.AppendLine("Responde SIEMPRE con un único objeto JSON, sin texto alrededor, con una de estas dos formas:");
        constructor.AppendLine("Para usar una herramienta:");
        constructor.AppendLine("{\"accion\":\"herramienta\",\"herramienta\":\"<nombre>\",\"argumentos\":{...}}");
        constructor.AppendLine("Para dar la respuesta final:");
        constructor.AppendLine("{\"accion\":\"responder\",\"respuesta\":\"<texto>\"}");
        constructor.AppendLine();
        constructor.AppendLine("Reglas:");
        constructor.AppendLine("1. Usa una herramienta por vez y espera su resultado antes de decidir el paso siguiente.");
        constructor.AppendLine("2. No inventes datos: si necesitas un dato, obtenlo con una herramienta.");
        constructor.AppendLine("3. Si las herramientas no alcanzan para responder, dilo con claridad.");
        constructor.AppendLine("4. Responde en español, de forma breve y concreta.");

        return constructor.ToString();
    }

    private RespuestaAgente Construir(
        ConsultaAgente consulta,
        string respuesta,
        List<PasoEjecutado> pasos,
        List<AccionPendiente> pendientes,
        bool alcanzoLimite,
        int tokensEntrada,
        int tokensSalida,
        Stopwatch cronometro) => new()
        {
            Pregunta = consulta.Pregunta,
            Respuesta = respuesta,
            Pasos = pasos,
            AccionesPendientes = pendientes,
            AlcanzoLimite = alcanzoLimite,
            ProveedorLenguaje = proveedor.Nombre,
            TokensEntrada = tokensEntrada,
            TokensSalida = tokensSalida,
            MilisegundosTotales = (int)cronometro.ElapsedMilliseconds
        };
}
