using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>
/// Configuración de registros de hábitos de alimentación/hidratación en el
/// schema <c>app</c> (SPEC §18, B): una fila por <c>(paciente, plantilla de
/// hábito, fecha local)</c> — único <c>uq_habit_checks_patient_template_date</c> —
/// con el mismo contrato de columnas que la dimensión de nutrición del Índice
/// de Salud consume (SPEC §13.4.3: <c>patient_id</c>, <c>habit_template_id</c>,
/// <c>local_date</c>, <c>is_done</c>).
/// </summary>
public sealed class HabitCheckConfiguration : IEntityTypeConfiguration<HabitCheck>
{
    public void Configure(EntityTypeBuilder<HabitCheck> builder)
    {
        builder.ToTable("habit_checks", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.HabitTemplateId)
            .HasColumnName("habit_template_id");

        builder.Property(x => x.LocalDate)
            .HasColumnName("local_date")
            .HasColumnType("date");

        builder.Property(x => x.IsDone)
            .HasColumnName("is_done")
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(x => x.PatientId)
            .HasDatabaseName("ix_habit_checks_patient_id");

        builder.HasIndex(x => x.HabitTemplateId)
            .HasDatabaseName("ix_habit_checks_habit_template_id");

        builder.HasIndex(x => x.LocalDate)
            .HasDatabaseName("ix_habit_checks_local_date");

        builder.HasIndex(x => new { x.PatientId, x.HabitTemplateId, x.LocalDate })
            .HasDatabaseName("uq_habit_checks_patient_template_date")
            .IsUnique();

        // Relationships
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HabitTemplate)
            .WithMany(h => h.HabitChecks)
            .HasForeignKey(x => x.HabitTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}