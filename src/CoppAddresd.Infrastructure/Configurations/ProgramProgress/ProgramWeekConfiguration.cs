using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>Configuración de semanas del programa en el schema <c>app</c>.</summary>
public sealed class ProgramWeekConfiguration : IEntityTypeConfiguration<ProgramWeek>
{
    public void Configure(EntityTypeBuilder<ProgramWeek> builder)
    {
        builder.ToTable("program_weeks", "app", t =>
        {
            t.HasCheckConstraint("ck_program_weeks_week_number_positive", "\"week_number\" >= 1");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.EnrollmentId)
            .HasColumnName("enrollment_id");

        builder.Property(x => x.WeekNumber)
            .HasColumnName("week_number");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(ProgramWeekStatus.Locked);

        builder.Property(x => x.WeekStartDateLocal)
            .HasColumnName("week_start_date_local")
            .HasColumnType("date");

        builder.Property(x => x.WeekEndDateLocal)
            .HasColumnName("week_end_date_local")
            .HasColumnType("date");

        builder.Property(x => x.TasksSnapshot)
            .HasColumnName("tasks_snapshot")
            .HasColumnType("jsonb");

        builder.Property(x => x.TemplateVersionAtStart)
            .HasColumnName("template_version_at_start");

        builder.Property(x => x.ActivatedAt)
            .HasColumnName("activated_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.EnrollmentId)
            .HasDatabaseName("ix_program_weeks_enrollment_id");

        builder.HasIndex(x => new { x.EnrollmentId, x.WeekNumber })
            .HasDatabaseName("uq_program_weeks_enrollment_week")
            .IsUnique();

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_program_weeks_status");

        builder.HasIndex(x => x.WeekStartDateLocal)
            .HasDatabaseName("ix_program_weeks_week_start_date_local");

        // Relationships
        builder.HasMany(x => x.DailyCheckIns)
            .WithOne(x => x.Week)
            .HasForeignKey(x => x.ProgramWeekId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.TaskCompletions)
            .WithOne(x => x.Week)
            .HasForeignKey(x => x.ProgramWeekId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}