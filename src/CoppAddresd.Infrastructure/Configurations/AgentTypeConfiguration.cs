using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de tipos de agente en el schema <c>agents</c>.</summary>
public sealed class AgentTypeConfiguration : IEntityTypeConfiguration<AgentType>
{
    public void Configure(EntityTypeBuilder<AgentType> builder)
    {
        builder.ToTable("agent_types", "agents");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(x => x.Specialty)
            .HasColumnName("specialty")
            .HasMaxLength(120);

        builder.Property(x => x.IconKey)
            .HasColumnName("icon_key")
            .HasMaxLength(64);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        builder.Property(x => x.ActiveVersionId)
            .HasColumnName("active_version_id");

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        builder.HasOne(x => x.ActiveVersion)
            .WithMany()
            .HasForeignKey(x => x.ActiveVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_agent_types_active_version");

        builder.HasMany(x => x.Versions)
            .WithOne(v => v.AgentType)
            .HasForeignKey(v => v.AgentTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ActiveVersionId)
            .HasDatabaseName("ix_agent_types_active_version_id");
    }
}