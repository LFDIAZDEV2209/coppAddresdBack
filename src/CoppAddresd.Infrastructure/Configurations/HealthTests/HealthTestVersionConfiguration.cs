using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de versiones de instrumentos en el schema <c>app</c>.</summary>
public sealed class HealthTestVersionConfiguration : IEntityTypeConfiguration<HealthTestVersion>
{
    public void Configure(EntityTypeBuilder<HealthTestVersion> builder)
    {
        builder.ToTable("health_test_versions", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id");

        builder.Property(x => x.VersionNumber).HasColumnName("version_number");

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);

        builder
            .Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue(HealthTestVersionStatus.draft)
            .HasConversion<string>();

        builder.Property(x => x.IsCurrent).HasColumnName("is_current").HasDefaultValue(false);

        builder
            .Property(x => x.ScoringStrategy)
            .HasColumnName("scoring_strategy")
            .HasMaxLength(30)
            .HasDefaultValue(HealthTestScoringStrategy.sum)
            .HasConversion<string>();

        builder.Property(x => x.Points).HasColumnName("points");

        builder
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder
            .Property(x => x.PublishedAt)
            .HasColumnName("published_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.RetiredAt).HasColumnName("retired_at").HasColumnType("timestamptz");

        // Indexes
        builder
            .HasIndex(x => x.InstrumentId)
            .HasDatabaseName("ix_health_test_versions_instrument_id");

        builder
            .HasIndex(x => new { x.InstrumentId, x.VersionNumber })
            .IsUnique()
            .HasDatabaseName("ix_health_test_versions_instrument_number");

        builder
            .HasIndex(x => new { x.InstrumentId, x.IsCurrent })
            .HasDatabaseName("ix_health_test_versions_instrument_current");

        builder.HasIndex(x => x.Status).HasDatabaseName("ix_health_test_versions_status");

        // Relationships
        builder
            .HasOne(x => x.Instrument)
            .WithMany(x => x.Versions)
            .HasForeignKey(x => x.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
