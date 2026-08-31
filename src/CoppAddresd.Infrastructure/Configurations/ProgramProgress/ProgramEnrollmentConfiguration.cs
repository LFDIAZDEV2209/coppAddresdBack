using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>
/// Configuración de inscripciones al programa en el schema <c>app</c>.
/// El índice único filtrado garantiza una sola inscripción activa por paciente.
/// </summary>
public sealed class ProgramEnrollmentConfiguration : IEntityTypeConfiguration<ProgramEnrollment>
{
    public void Configure(EntityTypeBuilder<ProgramEnrollment> builder)
    {
        builder.ToTable("program_enrollments", "app", t =>
        {
            t.HasCheckConstraint("ck_program_enrollments_current_week_positive", "\"current_week_number\" >= 1");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.TemplateId)
            .HasColumnName("template_id");

        builder.Property(x => x.Timezone)
            .HasColumnName("timezone")
            .HasMaxLength(64)
            .HasDefaultValue("America/Bogota");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(ProgramEnrollmentStatus.Active);

        builder.Property(x => x.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.StartLocalDate)
            .HasColumnName("start_local_date")
            .HasColumnType("date");

        builder.Property(x => x.CurrentWeekNumber)
            .HasColumnName("current_week_number")
            .HasDefaultValue(1);

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.PausedAt)
            .HasColumnName("paused_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.WithdrawnAt)
            .HasColumnName("withdrawn_at")
            .HasColumnType("timestamptz");

        // Los actores (created_by/updated_by) apuntan a auth.users; la FK se
        // crea por SQL en la migración (fuera del modelo EF).
        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(x => x.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.TemplateId)
            .HasDatabaseName("ix_program_enrollments_template_id");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_program_enrollments_status");

        // Una inscripción activa por paciente (las pausadas/retiradas no bloquean re-inscripción).
        // Nota: EF Core deduplica índices por set de columnas (no se pueden modelar
        // dos índices sobre patient_id), por lo que el índice simple
        // ix_program_enrollments_patient_id (SPEC §3.3) se crea por SQL con
        // CREATE INDEX IF NOT EXISTS en la migración AddProgramProgressSafety.
        builder.HasIndex(x => x.PatientId)
            .HasDatabaseName("uq_program_enrollments_patient_active")
            .IsUnique()
            .HasFilter("\"status\" = 'Active'");

        // Clave alternativa (id, patient_id) que respalda la FK compuesta de
        // emotional_records: un registro emocional no puede referenciar la
        // inscripción de otro paciente (SPEC §3.10 invariante cross-paciente).
        builder.HasAlternateKey(x => new { x.Id, x.PatientId })
            .HasName("uq_program_enrollments_id_patient");

        // Relationships
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Template)
            .WithMany()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StreakState)
            .WithOne(x => x.Enrollment)
            .HasForeignKey<StreakState>(x => x.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Weeks)
            .WithOne(x => x.Enrollment)
            .HasForeignKey(x => x.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.DailyCheckIns)
            .WithOne(x => x.Enrollment)
            .HasForeignKey(x => x.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.TaskCompletions)
            .WithOne(x => x.Enrollment)
            .HasForeignKey(x => x.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.XpLedgerEntries)
            .WithOne(x => x.Enrollment)
            .HasForeignKey(x => x.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.StreakFreezes)
            .WithOne(x => x.Enrollment)
            .HasForeignKey(x => x.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Adaptations)
            .WithOne(x => x.Enrollment)
            .HasForeignKey(x => x.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK compuesta (program_enrollment_id, patient_id) → program_enrollments(id, patient_id):
        // con MATCH SIMPLE, si program_enrollment_id es NULL la FK no se aplica,
        // pero cuando está presente el patient_id debe coincidir con el de la
        // inscripción. ON DELETE RESTRICT: las inscripciones nunca se borran
        // físicamente (el retiro es por estado y conserva historial), por lo que
        // el historial emocional queda protegido sin necesidad de SET NULL.
        builder.HasMany(x => x.EmotionalRecords)
            .WithOne(x => x.Enrollment)
            .HasPrincipalKey(x => new { x.Id, x.PatientId })
            .HasForeignKey(x => new { x.ProgramEnrollmentId, x.PatientId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}