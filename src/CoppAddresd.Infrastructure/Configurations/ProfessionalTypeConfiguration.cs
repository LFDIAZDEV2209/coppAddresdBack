using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración del catálogo de profesiones en el schema <c>erp</c>.</summary>
public sealed class ProfessionalTypeConfiguration : IEntityTypeConfiguration<ProfessionalType>
{
    public void Configure(EntityTypeBuilder<ProfessionalType> builder)
    {
        builder.ToTable("professional_types", "erp");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();

        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);

        builder.Property(x => x.SortOrder).HasColumnName("sort_order");

        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => x.Code).HasDatabaseName("ix_professional_types_code").IsUnique();
    }
}
