using CoppAddresd.Domain.Entities.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>Mapeo del catálogo de asignación en el esquema app con JSON nativo de Npgsql.</summary>
public sealed class ProgramAssignmentRuleConfiguration : IEntityTypeConfiguration<ProgramAssignmentRule>
{
    public void Configure(EntityTypeBuilder<ProgramAssignmentRule> builder)
    {
        builder.ToTable("program_assignment_rules", "app");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(80);
        builder.Property(x => x.ConditionCode).HasColumnName("condition_code").HasMaxLength(80);
        builder.Property(x => x.Target).HasColumnName("target").HasColumnType("jsonb");
        builder.Property(x => x.Priority).HasColumnName("priority");
        builder.Property(x => x.Weight).HasColumnName("weight").HasPrecision(10, 4).HasDefaultValue(1m);
        builder.Property(x => x.Criteria).HasColumnName("criteria").HasColumnType("jsonb");
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ix_program_assignment_rules_code");
        builder.HasOne<ProgramCondition>().WithMany().HasForeignKey(x => x.ConditionCode)
            .HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ConditionCode).HasDatabaseName("ix_program_assignment_rules_condition_code");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
    }
}
