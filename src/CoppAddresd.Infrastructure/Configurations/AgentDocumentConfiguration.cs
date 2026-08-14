using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de documentos de conocimiento en el schema <c>agents</c>.</summary>
public sealed class AgentDocumentConfiguration : IEntityTypeConfiguration<AgentDocument>
{
    public void Configure(EntityTypeBuilder<AgentDocument> builder)
    {
        builder.ToTable("documents", "agents");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.KnowledgeBaseId)
            .HasColumnName("knowledge_base_id");

        builder.Property(x => x.StorageKey)
            .HasColumnName("storage_key")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(120);

        builder.Property(x => x.FileSizeBytes)
            .HasColumnName("file_size_bytes");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text");

        builder.Property(x => x.ChunksCount)
            .HasColumnName("chunks_count");

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        builder.HasOne(x => x.KnowledgeBase)
            .WithMany(kb => kb.Documents)
            .HasForeignKey(x => x.KnowledgeBaseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Listado de documentos por knowledge base (dashboard + re-procesar).
        builder.HasIndex(x => x.KnowledgeBaseId)
            .HasDatabaseName("ix_documents_knowledge_base_id");
    }
}