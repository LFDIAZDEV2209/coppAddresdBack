using CoppAddresd.Domain.Entities.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>Configuración del estado de racha (1:1 con la inscripción) en el schema <c>app</c>.</summary>
public sealed class StreakStateConfiguration : IEntityTypeConfiguration<StreakState>
{
    public void Configure(EntityTypeBuilder<StreakState> builder)
    {
        builder.ToTable("streak_states", "app", t =>
        {
            t.HasCheckConstraint(
                "ck_streak_states_freezes_remaining_range",
                "\"freezes_remaining\" >= 0 AND \"freezes_remaining\" <= 3");
            t.HasCheckConstraint(
                "ck_streak_states_multiplier_active_positive",
                "\"multiplier_active\" >= 1.0");
        });

        // Una fila por inscripción: la PK es el FK a program_enrollments.
        builder.HasKey(x => x.EnrollmentId);
        builder.Property(x => x.EnrollmentId)
            .HasColumnName("enrollment_id");

        builder.Property(x => x.CurrentStreak)
            .HasColumnName("current_streak")
            .HasDefaultValue(0);

        builder.Property(x => x.LongestStreak)
            .HasColumnName("longest_streak")
            .HasDefaultValue(0);

        builder.Property(x => x.LastActiveDate)
            .HasColumnName("last_active_date")
            .HasColumnType("date");

        builder.Property(x => x.FreezesRemaining)
            .HasColumnName("freezes_remaining")
            .HasDefaultValue(0);

        builder.Property(x => x.FreezesUsedTotal)
            .HasColumnName("freezes_used_total")
            .HasDefaultValue(0);

        builder.Property(x => x.LastBreakDate)
            .HasColumnName("last_break_date")
            .HasColumnType("date");

        // Multiplicador x2 por hito de racha (SPEC §16): DECIMAL(4,2) default
        // 1.0 (= sin multiplicador) + fin de vigencia timestamptz nullable.
        // Un multiplicador vencido se trata como 1.0 y se resetea lazy en el
        // próximo otorgamiento (SPEC §16, C.1).
        builder.Property(x => x.MultiplierActive)
            .HasColumnName("multiplier_active")
            .HasPrecision(4, 2)
            .HasDefaultValue(1.0m);

        builder.Property(x => x.MultiplierEndsAt)
            .HasColumnName("multiplier_ends_at")
            .HasColumnType("timestamptz");

        // Racha propia del nutracéutico (SPEC §19, A): SMALLINT NOT NULL
        // default 0 + fecha local del último día que aportó. Se mantiene e
        // incrementa SOLO al completar la tarea nutraceutico (independiente de
        // la racha general; un día perdido la rompe, los congelamientos NO la
        // protegen — AC-38).
        builder.Property(x => x.NbCurrentStreak)
            .HasColumnName("nb_current_streak")
            .HasDefaultValue((short)0);

        builder.Property(x => x.NbLongestStreak)
            .HasColumnName("nb_longest_streak")
            .HasDefaultValue((short)0);

        builder.Property(x => x.NbLastCompletedDate)
            .HasColumnName("nb_last_completed_date")
            .HasColumnType("date");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.LastActiveDate)
            .HasDatabaseName("ix_streak_states_last_active_date");
    }
}