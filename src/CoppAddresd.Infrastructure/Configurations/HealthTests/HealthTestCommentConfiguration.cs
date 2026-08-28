using CoppAddresd.Domain.Entities.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de comentarios de tests en el schema <c>app</c>.</summary>
public sealed class HealthTestCommentConfiguration : IEntityTypeConfiguration<HealthTestComment>
{
    public void Configure(EntityTypeBuilder<HealthTestComment> builder)
    {
        builder.ToTable("health_test_comments", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId).HasColumnName("patient_id");

        builder.Property(x => x.EvaluationId).HasColumnName("evaluation_id");

        builder.Property(x => x.AuthorId).HasColumnName("author_id");

        builder.Property(x => x.Body).HasColumnName("body");

        builder
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(x => x.PatientId).HasDatabaseName("ix_health_test_comments_patient_id");

        builder
            .HasIndex(x => x.EvaluationId)
            .HasDatabaseName("ix_health_test_comments_evaluation_id");

        // Relationships
        builder
            .HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Evaluation)
            .WithMany()
            .HasForeignKey(x => x.EvaluationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
