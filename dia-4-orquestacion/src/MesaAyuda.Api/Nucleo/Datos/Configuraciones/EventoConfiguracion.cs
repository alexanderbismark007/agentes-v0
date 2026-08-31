using MesaAyuda.Api.Dominio.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MesaAyuda.Api.Nucleo.Datos.Configuraciones;

public sealed class EventoConfiguracion : IEntityTypeConfiguration<EventoAuditoria>
{
    public void Configure(EntityTypeBuilder<EventoAuditoria> constructor)
    {
        constructor.ToTable("eventos");
        constructor.HasKey(e => e.Id);
        constructor.Property(e => e.Id).ValueGeneratedNever();

        constructor.Property(e => e.Tipo).HasMaxLength(60).IsRequired();
        constructor.Property(e => e.Origen).HasMaxLength(60).IsRequired();
        constructor.Property(e => e.Huella).HasMaxLength(64).IsRequired();
        constructor.Property(e => e.Carga).HasMaxLength(4000).IsRequired();
        constructor.Property(e => e.Referencia).HasMaxLength(120);
        constructor.Property(e => e.Detalle).HasMaxLength(1000);

        constructor.Property(e => e.Resultado).HasConversion<string>().HasMaxLength(20).IsRequired();

        // La deteccion de duplicados consulta por huella y resultado; el indice
        // sostiene esa consulta, que ocurre en cada evento entrante.
        constructor.HasIndex(e => new { e.Huella, e.Resultado });
        constructor.HasIndex(e => e.FechaCreacion);
    }
}
