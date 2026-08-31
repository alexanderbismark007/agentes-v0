using MesaAyuda.Api.Dominio.Conocimiento;

namespace MesaAyuda.Api.Modulos.Conocimiento.Contratos;

/// <summary>
/// Consulta en lenguaje natural sobre los documentos indexados.
/// </summary>
public sealed record ConsultaDocumental
{
    public string Pregunta { get; init; } = string.Empty;

    /// <summary>
    /// Nivel de acceso del solicitante. Determina qué documentos pueden
    /// consultarse; por defecto solo los públicos.
    /// </summary>
    public NivelAcceso NivelAcceso { get; init; } = NivelAcceso.Publico;
}

/// <summary>
/// Fragmento citado como respaldo de la respuesta.
/// </summary>
public sealed record FuenteCitada
{
    /// <summary>Número con el que la respuesta se refiere a esta fuente.</summary>
    public required int Numero { get; init; }

    public required Guid DocumentoId { get; init; }

    public required string Documento { get; init; }

    public string? Referencia { get; init; }

    /// <summary>Porción del fragmento, para verificar la cita sin abrir el documento.</summary>
    public required string Extracto { get; init; }

    public required double Similitud { get; init; }
}

/// <summary>
/// Respuesta a una consulta documental, con todo lo necesario para auditarla.
/// </summary>
public sealed record RespuestaConsulta
{
    public required string Pregunta { get; init; }

    public required string Respuesta { get; init; }

    /// <summary>
    /// Indica si la respuesta se apoya en fragmentos recuperados. Cuando es
    /// falso, el sistema declaró no tener información en lugar de improvisar.
    /// </summary>
    public required bool TieneRespaldo { get; init; }

    public required IReadOnlyCollection<FuenteCitada> Fuentes { get; init; }

    /// <summary>Similitud del mejor fragmento recuperado.</summary>
    public required double ConfianzaRecuperacion { get; init; }

    public required string ProveedorLenguaje { get; init; }

    public required string ProveedorEmbeddings { get; init; }

    public string? ModeloLenguaje { get; init; }

    public int TokensEntrada { get; init; }

    public int TokensSalida { get; init; }

    public required int MilisegundosTotales { get; init; }
}

/// <summary>
/// Documento indexado, tal como lo devuelve la API.
/// </summary>
public sealed record DocumentoRespuesta
{
    public required Guid Id { get; init; }

    public required string Titulo { get; init; }

    public required string NombreArchivo { get; init; }

    public required string NivelAcceso { get; init; }

    public required int CantidadFragmentos { get; init; }

    public string? ModeloEmbeddings { get; init; }

    public required DateTimeOffset FechaActualizacion { get; init; }

    public static DocumentoRespuesta Desde(Documento documento) => new()
    {
        Id = documento.Id,
        Titulo = documento.Titulo,
        NombreArchivo = documento.NombreArchivo,
        NivelAcceso = documento.NivelAcceso.ToString(),
        CantidadFragmentos = documento.CantidadFragmentos,
        ModeloEmbeddings = documento.ModeloEmbeddings,
        FechaActualizacion = documento.FechaActualizacion
    };
}

/// <summary>
/// Resultado de indexar un documento.
/// </summary>
public sealed record ResultadoIndexacion
{
    public required Guid DocumentoId { get; init; }

    public required string Titulo { get; init; }

    public required int Fragmentos { get; init; }

    /// <summary>
    /// Verdadero cuando el documento ya estaba indexado con el mismo contenido
    /// y no hizo falta volver a procesarlo.
    /// </summary>
    public required bool SinCambios { get; init; }

    public required int MilisegundosTotales { get; init; }
}
