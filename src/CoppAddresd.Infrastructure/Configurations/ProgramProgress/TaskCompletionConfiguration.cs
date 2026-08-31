using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>
/// Configuración de completaciones de tarea en el schema <c>app</c>.
/// Las FKs de contenido son nullables (una por código de tarea) y usan
/// <see cref="DeleteBehavior.Restrict"/> para preservar la auditoría.
/// </summary>
public sealed class TaskCompletionConfiguration : IEntityTypeConfiguration<TaskCompletion>
{
    public void Configure(EntityTypeBuilder<TaskCompletion> builder)
    {
        builder.ToTable("task_completions", "app", t =>
        {
            t.HasCheckConstraint("ck_task_completions_weekday_range", "\"weekday\" BETWEEN 1 AND 7");
            t.HasCheckConstraint("ck_task_completions_plan_day_number_range", "\"nutrition_plan_day_number\" BETWEEN 1 AND 7");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.EnrollmentId)
            .HasColumnName("enrollment_id");

        builder.Property(x => x.ProgramWeekId)
            .HasColumnName("program_week_id");

        builder.Property(x => x.DailyCheckinId)
            .HasColumnName("daily_checkin_id");

        builder.Property(x => x.LocalDate)
            .HasColumnName("local_date")
            .HasColumnType("date");

        builder.Property(x => x.Weekday)
            .HasColumnName("weekday");

        builder.Property(x => x.TaskCode)
            .HasColumnName("task_code")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(x => x.PointsAwarded)
            .HasColumnName("points_awarded");

        builder.Property(x => x.ClientRequestId)
            .HasColumnName("client_request_id")
            .HasMaxLength(64);

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.ClientCompletedAt)
            .HasColumnName("client_completed_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.SourceRefType)
            .HasColumnName("source_ref_type")
            .HasMaxLength(20);

        builder.Property(x => x.ContentFingerprint)
            .HasColumnName("content_fingerprint")
            .HasMaxLength(64);

        // FKs de contenido (nullable, una por código de tarea).
        builder.Property(x => x.NutritionPlanId)
            .HasColumnName("nutrition_plan_id");

        builder.Property(x => x.NutritionPlanDayNumber)
            .HasColumnName("nutrition_plan_day_number");

        builder.Property(x => x.ExerciseRoutineId)
            .HasColumnName("exercise_routine_id");

        builder.Property(x => x.MediaId)
            .HasColumnName("media_id");

        builder.Property(x => x.VitalSignsBatchId)
            .HasColumnName("vital_signs_batch_id");

        builder.Property(x => x.NutribioticProductId)
            .HasColumnName("nutribiotic_product_id");

        builder.Property(x => x.EmotionalRecordId)
            .HasColumnName("emotional_record_id");

        // Indexes
        builder.HasIndex(x => x.EnrollmentId)
            .HasDatabaseName("ix_task_completions_enrollment_id");

        builder.HasIndex(x => x.DailyCheckinId)
            .HasDatabaseName("ix_task_completions_daily_checkin_id");

        builder.HasIndex(x => x.ProgramWeekId)
            .HasDatabaseName("ix_task_completions_program_week_id");

        // Idempotencia: una completación por (inscripción, fecha local, tarea).
        builder.HasIndex(x => new { x.EnrollmentId, x.LocalDate, x.TaskCode })
            .HasDatabaseName("uq_task_completions_enrollment_date_task")
            .IsUnique();

        builder.HasIndex(x => x.ClientRequestId)
            .HasDatabaseName("ix_task_completions_client_request_id");

        // Relationships (contenido en runtime; sin inversas en las entidades existentes).
        builder.HasOne(x => x.NutritionPlan)
            .WithMany()
            .HasForeignKey(x => x.NutritionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ExerciseRoutine)
            .WithMany()
            .HasForeignKey(x => x.ExerciseRoutineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Media)
            .WithMany()
            .HasForeignKey(x => x.MediaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.VitalSignsBatch)
            .WithMany()
            .HasForeignKey(x => x.VitalSignsBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        // NOTA: la SPEC §3.6 referencia app.products, pero el catálogo de
        // productos vive en el schema erp (ProductConfiguration). La FK apunta
        // a erp.products, único destino válido contra el esquema real.
        builder.HasOne(x => x.NutribioticProduct)
            .WithMany()
            .HasForeignKey(x => x.NutribioticProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EmotionalRecord)
            .WithMany()
            .HasForeignKey(x => x.EmotionalRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}