namespace MesaAyuda.Api.Modulos.Agente.Contratos;

/// <summary>
/// Consulta dirigida al agente.
/// </summary>
public sealed record ConsultaAgente
{
    public string Pregunta { get; init; } = string.Empty;

    /// <summary>
    /// Autoriza al agente a ejecutar herramientas que modifican datos. Es la
    /// aprobación humana: sin ella, el agente puede proponer un cambio pero no
    /// aplicarlo.
    /// </summary>
    public bool AutorizarEscritura { get; init; }
}

/// <summary>
/// Registro de una herramienta invocada durante el razonamiento.
/// </summary>
public sealed record PasoEjecutado
{
    public required int Numero { get; init; }

    public required string Herramienta { get; init; }

    /// <summary>Argumentos con los que se invocó, tal como los produjo el modelo.</summary>
    public required string Argumentos { get; init; }

    public required bool Exitosa { get; init; }

    /// <summary>Resumen de lo que devolvió, para el registro de auditoría.</summary>
    public string? Resultado { get; init; }

    public required int Milisegundos { get; init; }
}

/// <summary>
/// Acción que el agente propuso pero no ejecutó por falta de autorización.
/// </summary>
public sealed record AccionPendiente
{
    public required string Herramienta { get; init; }

    public required string Argumentos { get; init; }

    public required string Motivo { get; init; }
}

/// <summary>
/// Respuesta del agente, acompañada de todo el rastro de cómo llegó a ella.
///
/// La traza no es un detalle de depuración: es lo que permite auditar una
/// decisión automatizada. Sin ella, el agente sería una caja negra.
/// </summary>
public sealed record RespuestaAgente
{
    public required string Pregunta { get; init; }

    public required string Respuesta { get; init; }

    /// <summary>Herramientas efectivamente ejecutadas, en orden.</summary>
    public required IReadOnlyCollection<PasoEjecutado> Pasos { get; init; }

    /// <summary>Acciones que quedaron a la espera de aprobación humana.</summary>
    public required IReadOnlyCollection<AccionPendiente> AccionesPendientes { get; init; }

    /// <summary>
    /// Verdadero si el agente agotó el límite de iteraciones sin concluir. En
    /// ese caso la respuesta es parcial y debe tomarse con reservas.
    /// </summary>
    public required bool AlcanzoLimite { get; init; }

    public required string ProveedorLenguaje { get; init; }

    public required int TokensEntrada { get; init; }

    public required int TokensSalida { get; init; }

    public required int MilisegundosTotales { get; init; }
}

/// <summary>
/// Herramienta disponible, tal como se expone en la API.
/// </summary>
public sealed record HerramientaDisponible
{
    public required string Nombre { get; init; }

    public required string Descripcion { get; init; }

    public required string Riesgo { get; init; }

    public required bool RequiereAprobacion { get; init; }
}

/// <summary>
/// Reporte ejecutivo generado por el agente sobre el estado de la mesa de ayuda.
/// </summary>
public sealed record ReporteEjecutivo
{
    public required string Titulo { get; init; }

    public required string Contenido { get; init; }

    /// <summary>Datos objetivos sobre los que se elaboró el reporte.</summary>
    public required IReadOnlyCollection<PasoEjecutado> DatosConsultados { get; init; }

    public required DateTimeOffset GeneradoEn { get; init; }

    public required string ProveedorLenguaje { get; init; }
}
