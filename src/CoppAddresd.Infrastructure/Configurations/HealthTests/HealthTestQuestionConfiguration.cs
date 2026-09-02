using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de preguntas de tests en el schema <c>app</c>.</summary>
public sealed class HealthTestQuestionConfiguration : IEntityTypeConfiguration<HealthTestQuestion>
{
    public void Configure(EntityTypeBuilder<HealthTestQuestion> builder)
    {
        builder.ToTable("health_test_questions", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.VersionId).HasColumnName("version_id");

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(64);

        builder.Property(x => x.Section).HasColumnName("section").HasMaxLength(100);

        builder.Property(x => x.Text).HasColumnName("text");

        builder
            .Property(x => x.Type)
            .HasColumnName("type")
            .HasMaxLength(20)
            .HasDefaultValue(HealthTestQuestionType.scale)
            .HasConversion<string>();

        builder
            .Property(x => x.ScoringDirection)
            .HasColumnName("scoring_direction")
            .HasMaxLength(20)
            .HasDefaultValue(HealthTestScoringDirection.positive)
            .HasConversion<string>();

        builder.Property(x => x.SortOrder).HasColumnName("sort_order");

        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(20);

        builder.Property(x => x.MinValue).HasColumnName("min_value").HasColumnType("numeric");

        builder.Property(x => x.MaxValue).HasColumnName("max_value").HasColumnType("numeric");

        builder
            .Property(x => x.DefaultValue)
            .HasColumnName("default_value")
            .HasColumnType("numeric");

        builder.Property(x => x.MinLabel).HasColumnName("min_label").HasMaxLength(40);

        builder.Property(x => x.MaxLabel).HasColumnName("max_label").HasMaxLength(40);

        builder.Property(x => x.Hint).HasColumnName("hint");

        // Indexes
        builder.HasIndex(x => x.VersionId).HasDatabaseName("ix_health_test_questions_version_id");

        builder
            .HasIndex(x => new { x.VersionId, x.Code })
            .IsUnique()
            .HasDatabaseName("ix_health_test_questions_version_code");

        // Relationships
        builder
            .HasOne(x => x.Version)
            .WithMany(x => x.Questions)
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
