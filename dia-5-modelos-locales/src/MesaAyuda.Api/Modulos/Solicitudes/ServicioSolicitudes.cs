using MesaAyuda.Api.Dominio.Solicitudes;
using MesaAyuda.Api.Modulos.Solicitudes.Contratos;
using MesaAyuda.Api.Nucleo.Datos;
using MesaAyuda.Api.Nucleo.Errores;
using Microsoft.EntityFrameworkCore;

namespace MesaAyuda.Api.Modulos.Solicitudes;

/// <summary>
/// Coordina los casos de uso de solicitudes. Aquí vive la orquestación
/// (clasificar, generar código, persistir); las reglas del ciclo de vida
/// permanecen dentro de la entidad <see cref="Solicitud"/>.
/// </summary>
public sealed class ServicioSolicitudes(
    ContextoMesaAyuda contexto,
    IGeneradorCodigo generadorCodigo,
    IClasificadorSolicitudes clasificador,
    ILogger<ServicioSolicitudes> registro)
{
    /// <summary>
    /// Unidad responsable por defecto para cada categoría, usada cuando el
    /// solicitante no indica un destino explícito.
    /// </summary>
    private static readonly IReadOnlyDictionary<CategoriaSolicitud, string> UnidadPorCategoria =
        new Dictionary<CategoriaSolicitud, string>
        {
            [CategoriaSolicitud.Academica] = "Dirección de Carrera",
            [CategoriaSolicitud.Administrativa] = "Secretaría Académica",
            [CategoriaSolicitud.Tecnologica] = "Unidad de Sistemas",
            [CategoriaSolicitud.Financiera] = "Dirección Administrativa Financiera",
            [CategoriaSolicitud.Infraestructura] = "Unidad de Infraestructura",
            [CategoriaSolicitud.Otra] = "Mesa de Ayuda"
        };

    public async Task<SolicitudRespuesta> CrearAsync(CrearSolicitud peticion, CancellationToken cancelacion = default)
    {
        var sugerencia = await clasificador.ClasificarAsync(peticion.Titulo, peticion.Descripcion, cancelacion);

        // La categoría declarada por el solicitante manda; la sugerencia solo
        // completa el dato cuando no se indicó ninguna.
        var categoria = peticion.Categoria ?? sugerencia.Categoria;

        var unidad = string.IsNullOrWhiteSpace(peticion.UnidadDestino)
            ? UnidadPorCategoria[categoria]
            : peticion.UnidadDestino;

        var codigo = await generadorCodigo.SiguienteAsync(cancelacion);

        var solicitud = new Solicitud(
            codigo,
            peticion.Titulo,
            peticion.Descripcion,
            peticion.SolicitanteNombre,
            peticion.SolicitanteCorreo,
            unidad,
            categoria,
            peticion.Prioridad);

        solicitud.RegistrarSugerencia(sugerencia.Categoria, sugerencia.Confianza, sugerencia.Origen);

        contexto.Solicitudes.Add(solicitud);
        await contexto.SaveChangesAsync(cancelacion);

        registro.LogInformation(
            "Solicitud {Codigo} registrada en categoría {Categoria} (sugerida {Sugerida}, confianza {Confianza})",
            solicitud.Codigo, solicitud.Categoria, sugerencia.Categoria, sugerencia.Confianza);

        return SolicitudRespuesta.Desde(solicitud);
    }

    public async Task<PaginaRespuesta<SolicitudRespuesta>> ListarAsync(
        FiltroSolicitudes filtro,
        CancellationToken cancelacion = default)
    {
        var pagina = Math.Max(filtro.Pagina, 1);
        var tamano = Math.Clamp(filtro.TamanoPagina, 1, 100);

        var consulta = contexto.Solicitudes.AsNoTracking().AsQueryable();

        if (filtro.Estado.HasValue)
        {
            consulta = consulta.Where(s => s.Estado == filtro.Estado.Value);
        }

        if (filtro.Categoria.HasValue)
        {
            consulta = consulta.Where(s => s.Categoria == filtro.Categoria.Value);
        }

        if (filtro.Prioridad.HasValue)
        {
            consulta = consulta.Where(s => s.Prioridad == filtro.Prioridad.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            // Se normaliza a minúsculas en vez de usar una función propia de
            // PostgreSQL, para que la misma consulta se pueda ejecutar contra
            // cualquier proveedor de datos, incluido el usado en las pruebas.
            var patron = filtro.Texto.Trim().ToLower();

            consulta = consulta.Where(s =>
                s.Titulo.ToLower().Contains(patron) ||
                s.Descripcion.ToLower().Contains(patron) ||
                s.Codigo.ToLower().Contains(patron));
        }

        var total = await consulta.CountAsync(cancelacion);

        var elementos = await consulta
            .OrderByDescending(s => s.Prioridad)
            .ThenByDescending(s => s.FechaCreacion)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync(cancelacion);

        return new PaginaRespuesta<SolicitudRespuesta>
        {
            Elementos = elementos.Select(SolicitudRespuesta.Desde).ToArray(),
            Pagina = pagina,
            TamanoPagina = tamano,
            Total = total
        };
    }

    public async Task<SolicitudRespuesta> ObtenerAsync(Guid id, CancellationToken cancelacion = default)
    {
        var solicitud = await BuscarAsync(id, cancelacion);
        return SolicitudRespuesta.Desde(solicitud);
    }

    public async Task<SolicitudRespuesta> CambiarEstadoAsync(
        Guid id,
        CambiarEstado peticion,
        CancellationToken cancelacion = default)
    {
        var solicitud = await BuscarAsync(id, cancelacion);
        var estadoAnterior = solicitud.Estado;

        solicitud.CambiarEstado(peticion.NuevoEstado);

        if (!string.IsNullOrWhiteSpace(peticion.Motivo))
        {
            solicitud.AgregarComentario(
                "Sistema",
                $"Cambio de estado {estadoAnterior} a {peticion.NuevoEstado}. Motivo: {peticion.Motivo.Trim()}",
                esInterno: true);
        }

        await contexto.SaveChangesAsync(cancelacion);

        registro.LogInformation(
            "Solicitud {Codigo} pasó de {Anterior} a {Nuevo}",
            solicitud.Codigo, estadoAnterior, solicitud.Estado);

        return SolicitudRespuesta.Desde(solicitud);
    }

    public async Task<SolicitudRespuesta> ReasignarAsync(
        Guid id,
        ReasignarSolicitud peticion,
        CancellationToken cancelacion = default)
    {
        var solicitud = await BuscarAsync(id, cancelacion);

        solicitud.Reasignar(peticion.Categoria, peticion.Prioridad, peticion.UnidadDestino);
        await contexto.SaveChangesAsync(cancelacion);

        return SolicitudRespuesta.Desde(solicitud);
    }

    public async Task<ComentarioRespuesta> AgregarComentarioAsync(
        Guid id,
        CrearComentario peticion,
        CancellationToken cancelacion = default)
    {
        var solicitud = await BuscarAsync(id, cancelacion);

        var comentario = solicitud.AgregarComentario(peticion.Autor, peticion.Contenido, peticion.EsInterno);
        await contexto.SaveChangesAsync(cancelacion);

        return ComentarioRespuesta.Desde(comentario);
    }

    public async Task<IReadOnlyCollection<ComentarioRespuesta>> ListarComentariosAsync(
        Guid id,
        bool incluirInternos,
        CancellationToken cancelacion = default)
    {
        var existe = await contexto.Solicitudes.AnyAsync(s => s.Id == id, cancelacion);
        if (!existe)
        {
            throw new ExcepcionNoEncontrado("la solicitud", id);
        }

        var consulta = contexto.Comentarios.AsNoTracking().Where(c => c.SolicitudId == id);

        if (!incluirInternos)
        {
            consulta = consulta.Where(c => !c.EsInterno);
        }

        var comentarios = await consulta
            .OrderBy(c => c.FechaCreacion)
            .ToListAsync(cancelacion);

        return comentarios.Select(ComentarioRespuesta.Desde).ToArray();
    }

    private async Task<Solicitud> BuscarAsync(Guid id, CancellationToken cancelacion)
    {
        var solicitud = await contexto.Solicitudes
            .Include(s => s.Comentarios)
            .FirstOrDefaultAsync(s => s.Id == id, cancelacion);

        return solicitud ?? throw new ExcepcionNoEncontrado("la solicitud", id);
    }
}
