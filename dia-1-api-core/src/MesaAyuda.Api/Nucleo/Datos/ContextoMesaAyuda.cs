using MesaAyuda.Api.Dominio.Solicitudes;
using Microsoft.EntityFrameworkCore;

namespace MesaAyuda.Api.Nucleo.Datos;

/// <summary>
/// Contexto de persistencia de la mesa de ayuda.
/// </summary>
public class ContextoMesaAyuda(DbContextOptions<ContextoMesaAyuda> opciones) : DbContext(opciones)
{
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();

    public DbSet<ComentarioSolicitud> Comentarios => Set<ComentarioSolicitud>();

    protected override void OnModelCreating(ModelBuilder constructor)
    {
        constructor.HasDefaultSchema("mesa");
        constructor.ApplyConfigurationsFromAssembly(typeof(ContextoMesaAyuda).Assembly);
        base.OnModelCreating(constructor);
    }
}
