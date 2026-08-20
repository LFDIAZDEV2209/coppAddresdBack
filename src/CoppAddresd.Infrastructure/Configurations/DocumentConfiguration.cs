using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración del repositorio de documentos clínicos en el schema <c>app</c>.
/// Los FKs hacia auth.users (uploaded_by/created_by/updated_by/deleted_by) se
/// crean por SQL en la migración (fuera del modelo EF, patrón user_id).
/// </summary>
public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.ProfessionalId)
            .HasColumnName("professional_id");

        builder.Property(x => x.ClinicId)
            .HasColumnName("clinic_id");

        builder.Property(x => x.LocationId)
            .HasColumnName("location_id");

        // Columna preparada para Fase 6 (encounters); sin FK ni navegación
        // porque la entidad Encounter aún no existe.
        builder.Property(x => x.EncounterId)
            .HasColumnName("encounter_id");

        builder.Property(x => x.DocumentTypeId)
            .HasColumnName("document_type_id");

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(x => x.StorageKey)
            .HasColumnName("storage_key")
            .HasMaxLength(500);

        builder.Property(x => x.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(100);

        builder.Property(x => x.FileSizeBytes)
            .HasColumnName("file_size_bytes");

        builder.Property(x => x.Version)
            .HasColumnName("version")
            .HasDefaultValue(1);

        builder.Property(x => x.ParentDocumentId)
            .HasColumnName("parent_document_id");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue("Ready");

        builder.Property(x => x.UploadedBy)
            .HasColumnName("uploaded_by");

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(x => x.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(x => x.DeletedBy)
            .HasColumnName("deleted_by");

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Índices (regla anti-tenancy + FKs indexadas).
        builder.HasIndex(x => x.PatientId)
            .HasDatabaseName("ix_documents_patient_id");

        builder.HasIndex(x => x.ClinicId)
            .HasDatabaseName("ix_documents_clinic_id");

        builder.HasIndex(x => x.DocumentTypeId)
            .HasDatabaseName("ix_documents_document_type_id");

        builder.HasIndex(x => x.ParentDocumentId)
            .HasDatabaseName("ix_documents_parent_document_id");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_documents_status");

        builder.HasIndex(x => x.DeletedAt)
            .HasDatabaseName("ix_documents_deleted_at");

        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Professional)
            .WithMany()
            .HasForeignKey(x => x.ProfessionalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Clinic)
            .WithMany()
            .HasForeignKey(x => x.ClinicId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Location)
            .WithMany()
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.DocumentType)
            .WithMany()
            .HasForeignKey(x => x.DocumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Versions)
            .HasForeignKey(x => x.ParentDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}