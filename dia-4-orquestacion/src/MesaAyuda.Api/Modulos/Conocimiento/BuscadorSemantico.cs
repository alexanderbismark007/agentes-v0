using MesaAyuda.Api.Dominio.Conocimiento;
using MesaAyuda.Api.Nucleo.Datos;
using MesaAyuda.Api.Nucleo.Proveedores;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace MesaAyuda.Api.Modulos.Conocimiento;

/// <summary>
/// Fragmento recuperado junto con su grado de parecido con la consulta.
/// </summary>
/// <param name="FragmentoId">Identificador del fragmento.</param>
/// <param name="DocumentoId">Documento del que proviene.</param>
/// <param name="TituloDocumento">Título del documento, para citarlo.</param>
/// <param name="Referencia">Ubicación dentro del documento.</param>
/// <param name="Contenido">Texto del fragmento.</param>
/// <param name="Similitud">Parecido con la consulta, entre 0 y 1.</param>
public sealed record FragmentoRecuperado(
    Guid FragmentoId,
    Guid DocumentoId,
    string TituloDocumento,
    string? Referencia,
    string Contenido,
    double Similitud);

/// <summary>
/// Recupera los fragmentos más parecidos a una consulta.
///
/// Es el punto donde se decide qué información llega al modelo. Tener dos
/// implementaciones detrás de esta misma interfaz permite ejecutar el sistema
/// tanto sobre una base con pgvector como sin ella.
/// </summary>
public interface IBuscadorSemantico
{
    Task<IReadOnlyList<FragmentoRecuperado>> BuscarAsync(
        float[] vectorConsulta,
        int cantidad,
        NivelAcceso nivelMaximo,
        CancellationToken cancelacion = default);
}

/// <summary>
/// Búsqueda por vecino más cercano resuelta dentro de PostgreSQL mediante la
/// extensión pgvector.
///
/// El cálculo ocurre en el motor de base de datos y no en la aplicación: traer
/// todos los fragmentos a memoria para compararlos funciona con un reglamento,
/// pero no con un archivo institucional completo. Además, así la consulta puede
/// aprovechar un índice vectorial.
/// </summary>
public sealed class BuscadorSemanticoPostgres(ContextoMesaAyuda contexto) : IBuscadorSemantico
{
    public async Task<IReadOnlyList<FragmentoRecuperado>> BuscarAsync(
        float[] vectorConsulta,
        int cantidad,
        NivelAcceso nivelMaximo,
        CancellationToken cancelacion = default)
    {
        var vector = new Vector(vectorConsulta);

        // El operador <=> de pgvector devuelve la distancia coseno. Se ordena
        // de menor a mayor distancia y la similitud se obtiene restándola de 1.
        var filas = await contexto.Database
            .SqlQuery<FilaBusqueda>($"""
                SELECT  f."Id"            AS "FragmentoId",
                        f."DocumentoId"   AS "DocumentoId",
                        d."Titulo"        AS "TituloDocumento",
                        f."Referencia"    AS "Referencia",
                        f."Contenido"     AS "Contenido",
                        (f."Vector" <=> {vector}) AS "Distancia"
                FROM    mesa.fragmentos f
                JOIN    mesa.documentos d ON d."Id" = f."DocumentoId"
                WHERE   d."NivelAcceso" <= {(int)nivelMaximo}
                ORDER BY f."Vector" <=> {vector}
                LIMIT   {cantidad}
                """)
            .ToListAsync(cancelacion);

        return filas
            .Select(f => new FragmentoRecuperado(
                f.FragmentoId,
                f.DocumentoId,
                f.TituloDocumento,
                f.Referencia,
                f.Contenido,
                Similitud: 1d - f.Distancia))
            .ToArray();
    }

    /// <summary>
    /// Forma de cada fila devuelta por la consulta. Los nombres deben coincidir
    /// con los alias del SQL.
    /// </summary>
    private sealed record FilaBusqueda(
        Guid FragmentoId,
        Guid DocumentoId,
        string TituloDocumento,
        string? Referencia,
        string Contenido,
        double Distancia);
}

/// <summary>
/// Misma búsqueda resuelta en memoria, sin depender de la extensión de la base
/// de datos.
///
/// Se usa en las pruebas automatizadas y permite ejecutar el proyecto contra
/// una base sin pgvector. Produce el mismo resultado que la implementación
/// anterior, al costo de recorrer todos los fragmentos en cada consulta: sirve
/// para estudiar y para probar, no para producción.
/// </summary>
public sealed class BuscadorSemanticoEnMemoria(ContextoMesaAyuda contexto) : IBuscadorSemantico
{
    public async Task<IReadOnlyList<FragmentoRecuperado>> BuscarAsync(
        float[] vectorConsulta,
        int cantidad,
        NivelAcceso nivelMaximo,
        CancellationToken cancelacion = default)
    {
        var candidatos = await contexto.Fragmentos
            .AsNoTracking()
            .Join(
                contexto.Documentos.Where(d => d.NivelAcceso <= nivelMaximo),
                fragmento => fragmento.DocumentoId,
                documento => documento.Id,
                (fragmento, documento) => new
                {
                    fragmento.Id,
                    fragmento.DocumentoId,
                    documento.Titulo,
                    fragmento.Referencia,
                    fragmento.Contenido,
                    fragmento.Vector
                })
            .ToListAsync(cancelacion);

        return candidatos
            .Select(c => new FragmentoRecuperado(
                c.Id,
                c.DocumentoId,
                c.Titulo,
                c.Referencia,
                c.Contenido,
                Vectores.SimilitudCoseno(c.Vector, vectorConsulta)))
            .OrderByDescending(r => r.Similitud)
            .Take(cantidad)
            .ToArray();
    }
}
