using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de knowledge bases en el schema <c>agents</c>.</summary>
public sealed class KnowledgeBaseConfiguration : IEntityTypeConfiguration<KnowledgeBase>
{
    public void Configure(EntityTypeBuilder<KnowledgeBase> builder)
    {
        builder.ToTable("knowledge_bases", "agents");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(x => x.Scope)
            .HasColumnName("scope")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.AgentTypeId)
            .HasColumnName("agent_type_id");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        builder.HasOne(x => x.AgentType)
            .WithMany()
            .HasForeignKey(x => x.AgentTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_knowledge_bases_agent_type");

        // Búsqueda de KBs por alcance y por tipo de agente.
        builder.HasIndex(x => x.Scope)
            .HasDatabaseName("ix_knowledge_bases_scope");

        builder.HasIndex(x => x.AgentTypeId)
            .HasDatabaseName("ix_knowledge_bases_agent_type_id");
    }
}