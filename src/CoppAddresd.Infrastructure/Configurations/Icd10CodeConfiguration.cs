using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración del catálogo de códigos ICD-10 en el schema <c>app</c>.</summary>
public sealed class Icd10CodeConfiguration : IEntityTypeConfiguration<Icd10Code>
{
    public void Configure(EntityTypeBuilder<Icd10Code> builder)
    {
        builder.ToTable("icd10_codes", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(300);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => x.Code)
            .HasDatabaseName("ix_icd10_codes_code")
            .IsUnique();
    }
}