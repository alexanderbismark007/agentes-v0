using MesaAyuda.Api.Dominio.Conocimiento;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MesaAyuda.Api.Nucleo.Datos.Configuraciones;

public sealed class DocumentoConfiguracion : IEntityTypeConfiguration<Documento>
{
    public void Configure(EntityTypeBuilder<Documento> constructor)
    {
        constructor.ToTable("documentos");
        constructor.HasKey(d => d.Id);
        constructor.Property(d => d.Id).ValueGeneratedNever();

        constructor.Property(d => d.Titulo).HasMaxLength(200).IsRequired();
        constructor.Property(d => d.NombreArchivo).HasMaxLength(260).IsRequired();
        constructor.Property(d => d.HuellaContenido).HasMaxLength(64).IsRequired();
        constructor.Property(d => d.ModeloEmbeddings).HasMaxLength(60);

        // El nivel de acceso se guarda como numero porque las consultas filtran
        // por "hasta este nivel", una comparacion de orden y no de igualdad.
        constructor.Property(d => d.NivelAcceso).HasConversion<int>().IsRequired();

        constructor.HasIndex(d => d.NombreArchivo).IsUnique();

        constructor.HasMany(d => d.Fragmentos)
            .WithOne()
            .HasForeignKey(f => f.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);

        constructor.Navigation(d => d.Fragmentos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
