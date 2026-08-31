using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>Configuración de congelamientos de racha en el schema <c>app</c>.</summary>
public sealed class StreakFreezeConfiguration : IEntityTypeConfiguration<StreakFreeze>
{
    public void Configure(EntityTypeBuilder<StreakFreeze> builder)
    {
        builder.ToTable("streak_freezes", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.EnrollmentId)
            .HasColumnName("enrollment_id");

        builder.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(x => x.UsedOnLocalDate)
            .HasColumnName("used_on_local_date")
            .HasColumnType("date");

        builder.Property(x => x.GrantedAt)
            .HasColumnName("granted_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.GrantedReason)
            .HasColumnName("granted_reason")
            .HasMaxLength(40);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(x => x.EnrollmentId)
            .HasDatabaseName("ix_streak_freezes_enrollment_id");

        builder.HasIndex(x => x.Kind)
            .HasDatabaseName("ix_streak_freezes_kind");
    }
}