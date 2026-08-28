using CoppAddresd.Domain.Entities.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de respuestas de evaluaciones en el schema <c>app</c>.</summary>
public sealed class HealthTestResponseConfiguration : IEntityTypeConfiguration<HealthTestResponse>
{
    public void Configure(EntityTypeBuilder<HealthTestResponse> builder)
    {
        builder.ToTable("health_test_responses", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.EvaluationId).HasColumnName("evaluation_id");

        builder.Property(x => x.QuestionId).HasColumnName("question_id");

        builder.Property(x => x.AnswerOptionId).HasColumnName("answer_option_id");

        builder.Property(x => x.ValueText).HasColumnName("value_text");

        builder
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        // Indexes
        builder
            .HasIndex(x => x.EvaluationId)
            .HasDatabaseName("ix_health_test_responses_evaluation_id");

        builder
            .HasIndex(x => new { x.EvaluationId, x.QuestionId })
            .HasDatabaseName("ix_health_test_responses_evaluation_question");

        // Relationships
        builder
            .HasOne(x => x.Evaluation)
            .WithMany(x => x.Responses)
            .HasForeignKey(x => x.EvaluationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Question)
            .WithMany()
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.AnswerOption)
            .WithMany()
            .HasForeignKey(x => x.AnswerOptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
