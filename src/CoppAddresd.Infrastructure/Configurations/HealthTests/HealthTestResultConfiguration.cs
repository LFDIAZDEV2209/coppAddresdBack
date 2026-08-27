using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de resultados de evaluaciones en el schema <c>app</c>.</summary>
public sealed class HealthTestResultConfiguration : IEntityTypeConfiguration<HealthTestResult>
{
    public void Configure(EntityTypeBuilder<HealthTestResult> builder)
    {
        builder.ToTable("health_test_results", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.EvaluationId).HasColumnName("evaluation_id");

        builder
            .Property(x => x.ResultType)
            .HasColumnName("result_type")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(64);

        builder.Property(x => x.Label).HasColumnName("label").HasMaxLength(200);

        builder.Property(x => x.Value).HasColumnName("value").HasColumnType("numeric(10,2)");

        builder.Property(x => x.Qualifier).HasColumnName("qualifier").HasMaxLength(50);

        builder
            .Property(x => x.Severity)
            .HasColumnName("severity")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        // Indexes
        builder
            .HasIndex(x => x.EvaluationId)
            .HasDatabaseName("ix_health_test_results_evaluation_id");

        builder
            .HasIndex(x => new { x.EvaluationId, x.Code })
            .HasDatabaseName("ix_health_test_results_evaluation_code");

        // Relationships
        builder
            .HasOne(x => x.Evaluation)
            .WithMany(x => x.Results)
            .HasForeignKey(x => x.EvaluationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
