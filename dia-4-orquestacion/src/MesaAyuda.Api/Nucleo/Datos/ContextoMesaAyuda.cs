using MesaAyuda.Api.Dominio.Auditoria;
using MesaAyuda.Api.Dominio.Conocimiento;
using MesaAyuda.Api.Dominio.Solicitudes;
using MesaAyuda.Api.Nucleo.Configuracion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;

namespace MesaAyuda.Api.Nucleo.Datos;

/// <summary>
/// Contexto de persistencia de la mesa de ayuda.
/// </summary>
public class ContextoMesaAyuda(DbContextOptions<ContextoMesaAyuda> opciones) : DbContext(opciones)
{
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();

    public DbSet<ComentarioSolicitud> Comentarios => Set<ComentarioSolicitud>();

    public DbSet<Documento> Documentos => Set<Documento>();

    public DbSet<FragmentoDocumento> Fragmentos => Set<FragmentoDocumento>();

    public DbSet<EventoAuditoria> Eventos => Set<EventoAuditoria>();

    protected override void OnModelCreating(ModelBuilder constructor)
    {
        constructor.HasDefaultSchema("mesa");
        constructor.ApplyConfigurationsFromAssembly(typeof(ContextoMesaAyuda).Assembly);

        ConfigurarAlmacenamientoVectorial(constructor);

        base.OnModelCreating(constructor);
    }

    /// <summary>
    /// El almacenamiento de los vectores depende del motor de base de datos.
    ///
    /// Sobre PostgreSQL se usa la extensión pgvector, que aporta un tipo de dato
    /// propio y el operador de distancia que hace eficiente la búsqueda por
    /// similitud. Ese tipo no existe en otros proveedores, así que la traducción
    /// se declara únicamente cuando corresponde y el dominio sigue trabajando
    /// con un arreglo de números en todos los casos.
    /// </summary>
    private void ConfigurarAlmacenamientoVectorial(ModelBuilder constructor)
    {
        if (!Database.IsNpgsql())
        {
            return;
        }

        constructor.HasPostgresExtension("vector");

        constructor.Entity<FragmentoDocumento>()
            .Property(f => f.Vector)
            .HasColumnType($"vector({Dimensiones.Estandar})")
            .HasConversion(
                new ValueConverter<float[], Vector>(
                    valor => new Vector(valor),
                    valor => valor.ToArray()),

                // El comparador acompaña al conversor: sin él, el seguimiento de
                // cambios compararía los arreglos por referencia y no detectaría
                // una reindexación que cambia el contenido del vector.
                new ValueComparer<float[]>(
                    (primero, segundo) => primero != null && segundo != null && primero.SequenceEqual(segundo),
                    valor => valor.Aggregate(0, (acumulado, componente) => HashCode.Combine(acumulado, componente)),
                    valor => valor.ToArray()));
    }
}
