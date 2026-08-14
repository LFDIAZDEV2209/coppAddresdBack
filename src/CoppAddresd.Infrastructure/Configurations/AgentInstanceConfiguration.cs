using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración de instancias de agente en el schema <c>agents</c>.
/// El FK hacia auth.users se crea por SQL en la migración (fuera del modelo EF),
/// igual que en <c>erp.employees</c>.
/// </summary>
public sealed class AgentInstanceConfiguration : IEntityTypeConfiguration<AgentInstance>
{
    public void Configure(EntityTypeBuilder<AgentInstance> builder)
    {
        builder.ToTable("agent_instances", "agents");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.Property(x => x.AgentTypeId)
            .HasColumnName("agent_type_id");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

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
            .HasConstraintName("fk_agent_instances_agent_type");

        // Un paciente tiene a lo sumo UNA instancia por tipo de agente.
        builder.HasIndex(x => new { x.UserId, x.AgentTypeId })
            .HasDatabaseName("ix_agent_instances_user_id_agent_type_id")
            .IsUnique();

        builder.HasIndex(x => x.AgentTypeId)
            .HasDatabaseName("ix_agent_instances_agent_type_id");
    }
}