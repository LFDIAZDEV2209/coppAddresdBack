using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>
/// Configuración del registro de envíos de recordatorios de hito del programa
/// en el schema <c>app</c>. El índice único (enrollment_id, milestone_day)
/// respalda la idempotencia del job: cada hito de cada inscripción tiene como
/// máximo una fila (y por lo tanto un solo envío).
/// </summary>
public sealed class ProgramMilestoneSendConfiguration : IEntityTypeConfiguration<ProgramMilestoneSend>
{
    public void Configure(EntityTypeBuilder<ProgramMilestoneSend> builder)
    {
        builder.ToTable("program_milestone_sends", "app");

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
            .HasDefaultValue(ProgramMilestoneSendStatus.Pending);

        builder.Property(x => x.Attempts)
            .HasColumnName("attempts")
            .HasDefaultValue(0);

        builder.Property(x => x.ThreadId)
            .HasColumnName("thread_id")
            .HasMaxLength(255);

        builder.Property(x => x.SentAt)
            .HasColumnName("sent_at")
            .HasColumnType("timestamptz");

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
            .HasDatabaseName("ix_program_milestone_sends_enrollment_day")
            .IsUnique();

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_program_milestone_sends_status");

        // Relationships
        builder.HasOne(x => x.Enrollment)
            .WithMany()
            .HasForeignKey(x => x.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
