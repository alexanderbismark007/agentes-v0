using MesaAyuda.Api.Dominio.Solicitudes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MesaAyuda.Api.Nucleo.Datos.Configuraciones;

public sealed class SolicitudConfiguracion : IEntityTypeConfiguration<Solicitud>
{
    public void Configure(EntityTypeBuilder<Solicitud> constructor)
    {
        constructor.ToTable("solicitudes");
        constructor.HasKey(s => s.Id);

        // El identificador lo genera el dominio al construir la entidad, no la
        // base de datos. Declararlo evita que las entidades nuevas descubiertas
        // a traves de una navegacion se confundan con entidades ya existentes.
        constructor.Property(s => s.Id).ValueGeneratedNever();

        constructor.Property(s => s.Codigo).HasMaxLength(24).IsRequired();
        constructor.HasIndex(s => s.Codigo).IsUnique();

        constructor.Property(s => s.Titulo).HasMaxLength(180).IsRequired();
        constructor.Property(s => s.Descripcion).HasMaxLength(4000).IsRequired();
        constructor.Property(s => s.SolicitanteNombre).HasMaxLength(160).IsRequired();
        constructor.Property(s => s.SolicitanteCorreo).HasMaxLength(160).IsRequired();
        constructor.Property(s => s.UnidadDestino).HasMaxLength(120).IsRequired();
        constructor.Property(s => s.OrigenSugerencia).HasMaxLength(60);

        constructor.Property(s => s.Categoria).HasConversion<string>().HasMaxLength(30).IsRequired();
        constructor.Property(s => s.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();

        // La prioridad se guarda como número, no como texto: el listado ordena
        // por urgencia y un orden alfabético pondría "Alta" antes que "Critica".
        constructor.Property(s => s.Prioridad).HasConversion<int>().IsRequired();
        constructor.Property(s => s.CategoriaSugerida).HasConversion<string>().HasMaxLength(30);

        // Índices alineados a las consultas reales del listado y del tablero.
        constructor.HasIndex(s => s.Estado);
        constructor.HasIndex(s => new { s.Categoria, s.Prioridad });
        constructor.HasIndex(s => s.FechaCreacion);

        constructor.HasMany(s => s.Comentarios)
            .WithOne()
            .HasForeignKey(c => c.SolicitudId)
            .OnDelete(DeleteBehavior.Cascade);

        constructor.Navigation(s => s.Comentarios).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
