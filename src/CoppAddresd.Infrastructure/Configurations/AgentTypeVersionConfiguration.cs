using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de versiones de agentes en el schema <c>agents</c>.</summary>
public sealed class AgentTypeVersionConfiguration : IEntityTypeConfiguration<AgentTypeVersion>
{
    public void Configure(EntityTypeBuilder<AgentTypeVersion> builder)
    {
        builder.ToTable("agent_type_versions", "agents");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.AgentTypeId)
            .HasColumnName("agent_type_id");

        builder.Property(x => x.VersionNumber)
            .HasColumnName("version_number");

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active");

        builder.Property(x => x.Config)
            .HasColumnName("config")
            .HasColumnType("jsonb");

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasOne(x => x.AgentType)
            .WithMany(a => a.Versions)
            .HasForeignKey(x => x.AgentTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Números de versión únicos por tipo de agente.
        builder.HasIndex(x => new { x.AgentTypeId, x.VersionNumber })
            .HasDatabaseName("ix_agent_type_versions_agent_type_id_version_number")
            .IsUnique();

        // A lo sumo UNA versión activa por tipo de agente.
        builder.HasIndex(x => x.AgentTypeId)
            .HasDatabaseName("ix_agent_type_versions_active_per_type")
            .IsUnique()
            .HasFilter("\"is_active\"");
    }
}