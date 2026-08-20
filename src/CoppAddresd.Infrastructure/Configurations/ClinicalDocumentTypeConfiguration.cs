using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración del catálogo de tipos de documento clínico (schema <c>app</c>).
/// Las extensiones permitidas se persisten como <c>text[]</c> (lista de
/// valores, no una relación).
/// </summary>
public sealed class ClinicalDocumentTypeConfiguration : IEntityTypeConfiguration<ClinicalDocumentType>
{
    public void Configure(EntityTypeBuilder<ClinicalDocumentType> builder)
    {
        builder.ToTable("clinical_document_types", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.CategoryId)
            .HasColumnName("category_id");

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(x => x.AllowedExtensions)
            .HasColumnName("allowed_extensions");

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order")
            .HasDefaultValue(0);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.HasIndex(x => x.Code)
            .HasDatabaseName("ix_clinical_document_types_code")
            .IsUnique();

        builder.HasIndex(x => x.CategoryId)
            .HasDatabaseName("ix_clinical_document_types_category_id");

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Types)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}