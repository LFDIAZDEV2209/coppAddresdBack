using CoppAddresd.Domain.Entities.Redes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.Redes;

/// <summary>Catálogo de redes prestadoras en el schema <c>erp</c>.</summary>
public sealed class RedPrestadoraConfiguration : IEntityTypeConfiguration<RedPrestadora>
{
    public void Configure(EntityTypeBuilder<RedPrestadora> builder)
    {
        builder.ToTable("redes_prestadoras", "erp");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Nit)
            .HasColumnName("nit")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.RazonSocial)
            .HasColumnName("razon_social")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Direccion).HasColumnName("direccion").HasMaxLength(200);
        builder.Property(x => x.Ciudad).HasColumnName("ciudad").HasMaxLength(100);
        builder.Property(x => x.Departamento).HasColumnName("departamento").HasMaxLength(100);
        builder.Property(x => x.Telefono).HasColumnName("telefono").HasMaxLength(30);
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(150);
        builder.Property(x => x.CodigoPrestador).HasColumnName("codigo_prestador").HasMaxLength(30);
        builder.Property(x => x.Habilitacion).HasColumnName("habilitacion").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Naturaleza).HasColumnName("naturaleza").HasMaxLength(15).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => x.Nit)
            .HasDatabaseName("ix_redes_prestadoras_nit")
            .IsUnique();
    }
}
