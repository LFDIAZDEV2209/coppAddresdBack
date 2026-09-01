using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de rangos de interpretación de tests en el schema <c>app</c>.</summary>
public sealed class HealthTestScoreRangeConfiguration
    : IEntityTypeConfiguration<HealthTestScoreRange>
{
    public void Configure(EntityTypeBuilder<HealthTestScoreRange> builder)
    {
        builder.ToTable("health_test_score_ranges", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.VersionId).HasColumnName("version_id");

        builder.Property(x => x.MinValue).HasColumnName("min_value").HasColumnType("numeric(8,2)");

        builder.Property(x => x.MaxValue).HasColumnName("max_value").HasColumnType("numeric(8,2)");

        builder.Property(x => x.Label).HasColumnName("label").HasMaxLength(50);

        builder
            .Property(x => x.Severity)
            .HasColumnName("severity")
            .HasMaxLength(20)
            .HasDefaultValue(HealthTestSeverity.low)
            .HasConversion<string>();

        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        // Indexes
        builder
            .HasIndex(x => x.VersionId)
            .HasDatabaseName("ix_health_test_score_ranges_version_id");

        // Relationships
        builder
            .HasOne(x => x.Version)
            .WithMany(x => x.ScoreRanges)
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
