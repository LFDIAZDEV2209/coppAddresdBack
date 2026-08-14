using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración del catálogo de medicamentos en el schema <c>app</c>.</summary>
public sealed class MedicationConfiguration : IEntityTypeConfiguration<Medication>
{
    public void Configure(EntityTypeBuilder<Medication> builder)
    {
        builder.ToTable("medications", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Ndc)
            .HasColumnName("ndc")
            .HasMaxLength(30);

        builder.Property(x => x.RxNorm)
            .HasColumnName("rx_norm")
            .HasMaxLength(30);

        builder.Property(x => x.DrugClass)
            .HasColumnName("drug_class")
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => x.Name)
            .HasDatabaseName("ix_medications_name")
            .IsUnique();
    }
}