using CoppAddresd.Domain.Entities.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>Configuración de check-ins diarios del programa en el schema <c>app</c>.</summary>
public sealed class DailyCheckInConfiguration : IEntityTypeConfiguration<DailyCheckIn>
{
    public void Configure(EntityTypeBuilder<DailyCheckIn> builder)
    {
        builder.ToTable("daily_checkins", "app", t =>
        {
            t.HasCheckConstraint("ck_daily_checkins_weekday_range", "\"weekday\" BETWEEN 1 AND 7");
            t.HasCheckConstraint("ck_daily_checkins_mood_score_range", "\"mood_score\" BETWEEN 1 AND 5");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.EnrollmentId)
            .HasColumnName("enrollment_id");

        builder.Property(x => x.ProgramWeekId)
            .HasColumnName("program_week_id");

        builder.Property(x => x.LocalDate)
            .HasColumnName("local_date")
            .HasColumnType("date");

        builder.Property(x => x.Weekday)
            .HasColumnName("weekday");

        builder.Property(x => x.MoodScore)
            .HasColumnName("mood_score");

        builder.Property(x => x.Barriers)
            .HasColumnName("barriers")
            .HasMaxLength(40);

        builder.Property(x => x.TotalPoints)
            .HasColumnName("total_points")
            .HasDefaultValue(0);

        builder.Property(x => x.BonusAwarded)
            .HasColumnName("bonus_awarded")
            .HasDefaultValue(0);

        builder.Property(x => x.IsPerfectDay)
            .HasColumnName("is_perfect_day")
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.EnrollmentId)
            .HasDatabaseName("ix_daily_checkins_enrollment_id");

        builder.HasIndex(x => new { x.EnrollmentId, x.LocalDate })
            .HasDatabaseName("uq_daily_checkins_enrollment_date")
            .IsUnique();

        builder.HasIndex(x => x.ProgramWeekId)
            .HasDatabaseName("ix_daily_checkins_program_week_id");

        // Relationships
        builder.HasMany(x => x.TaskCompletions)
            .WithOne(x => x.DailyCheckIn)
            .HasForeignKey(x => x.DailyCheckinId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}