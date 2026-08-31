using MesaAyuda.Api.Dominio.Solicitudes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MesaAyuda.Api.Nucleo.Datos.Configuraciones;

public sealed class ComentarioConfiguracion : IEntityTypeConfiguration<ComentarioSolicitud>
{
    public void Configure(EntityTypeBuilder<ComentarioSolicitud> constructor)
    {
        constructor.ToTable("comentarios");
        constructor.HasKey(c => c.Id);
        constructor.Property(c => c.Id).ValueGeneratedNever();

        constructor.Property(c => c.Autor).HasMaxLength(160).IsRequired();
        constructor.Property(c => c.Contenido).HasMaxLength(2000).IsRequired();

        constructor.HasIndex(c => c.SolicitudId);
    }
}
