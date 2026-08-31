using MesaAyuda.Api.Dominio.Conocimiento;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MesaAyuda.Api.Nucleo.Datos.Configuraciones;

public sealed class FragmentoConfiguracion : IEntityTypeConfiguration<FragmentoDocumento>
{
    public void Configure(EntityTypeBuilder<FragmentoDocumento> constructor)
    {
        constructor.ToTable("fragmentos");
        constructor.HasKey(f => f.Id);
        constructor.Property(f => f.Id).ValueGeneratedNever();

        constructor.Property(f => f.Contenido).HasMaxLength(8000).IsRequired();
        constructor.Property(f => f.Referencia).HasMaxLength(120);
        constructor.Property(f => f.Orden).IsRequired();

        constructor.HasIndex(f => new { f.DocumentoId, f.Orden }).IsUnique();

        var vector = constructor.Property(f => f.Vector).IsRequired();

        // Sin un comparador explícito, el seguimiento de cambios compararía los
        // arreglos por referencia y no detectaría una reindexación que cambia el
        // contenido del vector manteniendo el mismo objeto.
        vector.Metadata.SetValueComparer(new ValueComparer<float[]>(
            (primero, segundo) => primero != null && segundo != null && primero.SequenceEqual(segundo),
            valor => valor.Aggregate(0, (acumulado, componente) => HashCode.Combine(acumulado, componente)),
            valor => valor.ToArray()));

        // El mapeo al tipo vectorial de PostgreSQL no se declara aquí, sino en
        // ContextoMesaAyuda, porque solo aplica a ese proveedor de datos.
    }
}
