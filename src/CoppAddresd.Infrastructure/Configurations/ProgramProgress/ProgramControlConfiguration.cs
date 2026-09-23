using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>
/// Configuración del control conversacional del programa
/// (<c>program_controls</c>) en el schema <c>app</c>. El índice único
/// (enrollment_id, milestone_day) respalda la idempotencia del job: cada hito
/// de cada inscripción tiene como máximo una fila (y por lo tanto un solo
/// envío). La columna <c>closed_reason</c> se valida con CHECK sobre los dos
/// motivos de cierre sin examen; <c>exam_batch_id</c> es una referencia
/// best-effort sin FK (el lote pertenece al módulo de exámenes).
/// </summary>
public sealed class ProgramControlConfiguration : IEntityTypeConfiguration<ProgramControl>
{
    public void Configure(EntityTypeBuilder<ProgramControl> builder)
    {
        builder.ToTable("program_controls", "app", t =>
        {
            // CHECK de closed_reason: solo los dos motivos de cierre sin examen.
            t.HasCheckConstraint(
                "CK_program_controls_closed_reason",
                "\"closed_reason\" IN ('declined', 'no_upload_timeout')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.EnrollmentId)
            .HasColumnName("enrollment_id");

        builder.Property(x => x.MilestoneDay)
            .HasColumnName("milestone_day");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(ProgramControlStatus.Pending);

        builder.Property(x => x.Attempts)
            .HasColumnName("attempts")
            .HasDefaultValue(0);

        builder.Property(x => x.ThreadId)
            .HasColumnName("thread_id")
            .HasMaxLength(255);

        builder.Property(x => x.SentAt)
            .HasColumnName("sent_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.RespondedAt)
            .HasColumnName("responded_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.FollowupSentAt)
            .HasColumnName("followup_sent_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.ClosedReason)
            .HasColumnName("closed_reason")
            .HasMaxLength(32);

        builder.Property(x => x.ExamBatchId)
            .HasColumnName("exam_batch_id")
            .HasColumnType("uuid");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Indexes: el único (enrollment_id, milestone_day) es la garantía de
        // idempotencia y a la vez cubre el lookup por inscripción del job.
        builder.HasIndex(x => new { x.EnrollmentId, x.MilestoneDay })
            .HasDatabaseName("ix_program_controls_enrollment_day")
            .IsUnique();

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_program_controls_status");

        // Relationships
        builder.HasOne(x => x.Enrollment)
            .WithMany()
            .HasForeignKey(x => x.EnrollmentId)
            .HasConstraintName("FK_program_controls_program_enrollments_enrollment_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}