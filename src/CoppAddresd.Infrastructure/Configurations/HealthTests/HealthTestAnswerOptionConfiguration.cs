using CoppAddresd.Domain.Entities.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de opciones de respuesta de tests en el schema <c>app</c>.</summary>
public sealed class HealthTestAnswerOptionConfiguration
    : IEntityTypeConfiguration<HealthTestAnswerOption>
{
    public void Configure(EntityTypeBuilder<HealthTestAnswerOption> builder)
    {
        builder.ToTable("health_test_answer_options", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.QuestionId).HasColumnName("question_id");

        builder.Property(x => x.Text).HasColumnName("text");

        builder
            .Property(x => x.ScoreValue)
            .HasColumnName("score_value")
            .HasColumnType("numeric(8,2)");

        builder.Property(x => x.DependsOnQuestionId).HasColumnName("depends_on_question_id");

        builder.Property(x => x.DependsOnOptionId).HasColumnName("depends_on_option_id");

        builder.Property(x => x.SortOrder).HasColumnName("sort_order");

        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        // Indexes
        builder
            .HasIndex(x => x.QuestionId)
            .HasDatabaseName("ix_health_test_answer_options_question_id");

        // Relationships
        builder
            .HasOne(x => x.Question)
            .WithMany(x => x.Options)
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
