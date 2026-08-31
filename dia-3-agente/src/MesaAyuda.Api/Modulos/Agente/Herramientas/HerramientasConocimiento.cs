using System.Text;
using System.Text.Json;
using MesaAyuda.Api.Dominio.Conocimiento;
using MesaAyuda.Api.Dominio.Solicitudes;
using MesaAyuda.Api.Modulos.Conocimiento;
using MesaAyuda.Api.Modulos.Solicitudes;
using MesaAyuda.Api.Modulos.Solicitudes.Contratos;
using MesaAyuda.Api.Nucleo.Errores;

namespace MesaAyuda.Api.Modulos.Agente.Herramientas;

/// <summary>
/// Da al agente acceso al reglamento institucional indexado en el día 2.
///
/// Reutiliza el mismo servicio de consultas, sin duplicar nada: el módulo de
/// conocimiento no sabe que existe un agente, y el agente lo usa como una
/// capacidad más entre otras.
/// </summary>
public sealed class HerramientaConsultarReglamento(ServicioConsultas servicio) : IHerramientaAgente
{
    public string Nombre => "consultar_reglamento";

    public string Descripcion =>
        "Consulta el reglamento y los documentos institucionales indexados. " +
        "Úsala para preguntas sobre plazos, requisitos, procedimientos, competencias " +
        "o cualquier norma. Devuelve la respuesta junto con los artículos que la respaldan.";

    public NivelRiesgo Riesgo => NivelRiesgo.Lectura;

    public object EsquemaParametros => new
    {
        type = "object",
        properties = new
        {
            pregunta = new
            {
                type = "string",
                description = "Pregunta concreta sobre el reglamento."
            }
        },
        required = new[] { "pregunta" }
    };

    public async Task<ResultadoHerramienta> EjecutarAsync(
        JsonElement argumentos,
        CancellationToken cancelacion = default)
    {
        var pregunta = Argumentos.Texto(argumentos, "pregunta");

        if (string.IsNullOrWhiteSpace(pregunta))
        {
            return ResultadoHerramienta.Fallida("no se indicó ninguna pregunta.");
        }

        // El agente opera del lado del personal de la mesa de ayuda, por lo que
        // puede acceder también a los documentos de circulación interna.
        var respuesta = await servicio.ResponderAsync(pregunta, NivelAcceso.Interno, cancelacion);

        if (!respuesta.TieneRespaldo)
        {
            return ResultadoHerramienta.Correcta(
                "El reglamento indexado no contiene información sobre esa consulta.",
                "sin respaldo documental");
        }

        var constructor = new StringBuilder();
        constructor.AppendLine(respuesta.Respuesta).AppendLine();
        constructor.AppendLine("Fuentes:");

        foreach (var fuente in respuesta.Fuentes)
        {
            constructor
                .Append('[').Append(fuente.Numero).Append("] ")
                .Append(fuente.Documento);

            if (!string.IsNullOrWhiteSpace(fuente.Referencia))
            {
                constructor.Append(", ").Append(fuente.Referencia);
            }

            constructor.Append(" (similitud ").Append(fuente.Similitud).AppendLine(")");
        }

        return ResultadoHerramienta.Correcta(
            constructor.ToString().TrimEnd(),
            $"{respuesta.Fuentes.Count} fuentes citadas");
    }
}

/// <summary>
/// Permite al agente obtener el expediente completo de una solicitud concreta.
/// </summary>
public sealed class HerramientaDetalleSolicitud(ServicioSolicitudes servicio) : IHerramientaAgente
{
    public string Nombre => "ver_solicitud";

    public string Descripcion =>
        "Devuelve el detalle completo de una solicitud a partir de su código, " +
        "por ejemplo SOL-2026-000003, incluidos sus comentarios internos. " +
        "Úsala cuando la consulta se refiera a un caso puntual identificado por su código.";

    public NivelRiesgo Riesgo => NivelRiesgo.Lectura;

    public object EsquemaParametros => new
    {
        type = "object",
        properties = new
        {
            codigo = new
            {
                type = "string",
                description = "Código de la solicitud, con el formato SOL-AAAA-NNNNNN."
            }
        },
        required = new[] { "codigo" }
    };

    public async Task<ResultadoHerramienta> EjecutarAsync(
        JsonElement argumentos,
        CancellationToken cancelacion = default)
    {
        var codigo = Argumentos.Texto(argumentos, "codigo")?.Trim();

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return ResultadoHerramienta.Fallida("no se indicó el código de la solicitud.");
        }

        // Se busca por código exacto usando el mismo filtro de texto del listado.
        var pagina = await servicio.ListarAsync(
            new FiltroSolicitudes { Texto = codigo, TamanoPagina = 5 },
            cancelacion);

        var solicitud = pagina.Elementos.FirstOrDefault(s =>
            string.Equals(s.Codigo, codigo, StringComparison.OrdinalIgnoreCase));

        if (solicitud is null)
        {
            return ResultadoHerramienta.Correcta(
                $"No existe ninguna solicitud con el código {codigo}.",
                "no encontrada");
        }

        var constructor = new StringBuilder();
        constructor.Append("Solicitud ").AppendLine(solicitud.Codigo);
        constructor.Append("- Título: ").AppendLine(solicitud.Titulo);
        constructor.Append("- Estado: ").AppendLine(solicitud.Estado);
        constructor.Append("- Categoría: ").AppendLine(solicitud.Categoria);
        constructor.Append("- Prioridad: ").AppendLine(solicitud.Prioridad);
        constructor.Append("- Unidad responsable: ").AppendLine(solicitud.UnidadDestino);
        constructor.Append("- Solicitante: ").Append(solicitud.SolicitanteNombre)
            .Append(" (").Append(solicitud.SolicitanteCorreo).AppendLine(")");
        constructor.Append("- Registrada el: ").AppendLine(solicitud.FechaCreacion.ToString("yyyy-MM-dd"));
        constructor.Append("- Descripción: ").AppendLine(solicitud.Descripcion);
        constructor.Append("- Transiciones posibles: ")
            .AppendLine(string.Join(", ", solicitud.TransicionesPermitidas));

        try
        {
            var comentarios = await servicio.ListarComentariosAsync(solicitud.Id, incluirInternos: true, cancelacion);

            if (comentarios.Count > 0)
            {
                constructor.AppendLine("- Comentarios:");

                foreach (var comentario in comentarios)
                {
                    constructor
                        .Append("  · ").Append(comentario.FechaCreacion.ToString("yyyy-MM-dd"))
                        .Append(' ').Append(comentario.Autor)
                        .Append(comentario.EsInterno ? " (interno): " : ": ")
                        .AppendLine(comentario.Contenido);
                }
            }
        }
        catch (ExcepcionNoEncontrado)
        {
            // La solicitud existe en el listado pero no en el detalle: no es un
            // fallo que deba interrumpir la respuesta del agente.
        }

        return ResultadoHerramienta.Correcta(constructor.ToString().TrimEnd(), solicitud.Codigo);
    }
}

/// <summary>
/// Única herramienta del agente que modifica datos, y por eso la única que
/// requiere aprobación humana antes de ejecutarse.
///
/// El agente puede proponerla, pero nunca aplicarla por su cuenta: quien decide
/// que una solicitud avance de estado sigue siendo una persona.
/// </summary>
public sealed class HerramientaCambiarEstado(ServicioSolicitudes servicio) : IHerramientaAgente
{
    public string Nombre => "cambiar_estado_solicitud";

    public string Descripcion =>
        "Cambia el estado de una solicitud. Esta acción modifica datos y requiere " +
        "aprobación de un operador antes de ejecutarse.";

    public NivelRiesgo Riesgo => NivelRiesgo.Escritura;

    public object EsquemaParametros => new
    {
        type = "object",
        properties = new
        {
            codigo = new
            {
                type = "string",
                description = "Código de la solicitud a modificar."
            },
            nuevoEstado = new
            {
                type = "string",
                description = "Estado al que debe pasar la solicitud.",
                @enum = Enum.GetNames<EstadoSolicitud>()
            },
            motivo = new
            {
                type = "string",
                description = "Justificación del cambio, que queda registrada en el expediente."
            }
        },
        required = new[] { "codigo", "nuevoEstado" }
    };

    public async Task<ResultadoHerramienta> EjecutarAsync(
        JsonElement argumentos,
        CancellationToken cancelacion = default)
    {
        var codigo = Argumentos.Texto(argumentos, "codigo")?.Trim();
        var nuevoEstado = Argumentos.Enumeracion<EstadoSolicitud>(argumentos, "nuevoEstado");

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return ResultadoHerramienta.Fallida("no se indicó el código de la solicitud.");
        }

        if (nuevoEstado is null)
        {
            return ResultadoHerramienta.Fallida("el estado indicado no es válido.");
        }

        var pagina = await servicio.ListarAsync(
            new FiltroSolicitudes { Texto = codigo, TamanoPagina = 5 },
            cancelacion);

        var solicitud = pagina.Elementos.FirstOrDefault(s =>
            string.Equals(s.Codigo, codigo, StringComparison.OrdinalIgnoreCase));

        if (solicitud is null)
        {
            return ResultadoHerramienta.Fallida($"no existe ninguna solicitud con el código {codigo}.");
        }

        try
        {
            var actualizada = await servicio.CambiarEstadoAsync(
                solicitud.Id,
                new CambiarEstado
                {
                    NuevoEstado = nuevoEstado.Value,
                    Motivo = Argumentos.Texto(argumentos, "motivo") ?? "Cambio solicitado a través del agente."
                },
                cancelacion);

            return ResultadoHerramienta.Correcta(
                $"La solicitud {actualizada.Codigo} pasó a estado {actualizada.Estado}.",
                $"{solicitud.Estado} -> {actualizada.Estado}");
        }
        catch (ExcepcionDominio excepcion)
        {
            // La máquina de estados del dominio sigue mandando: si la
            // transición no corresponde, se rechaza aunque la haya pedido el
            // agente y la haya aprobado un operador.
            return ResultadoHerramienta.Fallida(excepcion.Message);
        }
    }
}
