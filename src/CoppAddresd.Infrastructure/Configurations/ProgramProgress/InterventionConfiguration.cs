using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>
/// Configuración de las intervenciones en el schema <c>app</c> (SPEC §22,
/// "Paso 7d"): intervenciones derivadas de debilidades del paciente con
/// máquina de estados, XP acumulada y vínculo a telemedicina.
///
/// La columna <c>assigned_to</c> apunta a <c>auth.users</c>; la FK se crea por
/// SQL en la migración (patrón <c>user_id</c>, fuera del modelo EF), ON DELETE
/// SET NULL. La tabla SÍ se adjunta al trigger de auditoría (sin PHI:
/// ids, estados, timestamps, xp_awarded_total — precedente <c>notifications</c>).
/// </summary>
public sealed class InterventionConfiguration : IEntityTypeConfiguration<Intervention>
{
    public void Configure(EntityTypeBuilder<Intervention> builder)
    {
        builder.ToTable("interventions", "app", t =>
        {
            t.HasCheckConstraint("ck_interventions_type_values",
                "\"type\" IN ('nutrition_adjustment', 'exercise_adjustment', 'psychological_support', " +
                "'telehealth_nutrition', 'telehealth_medical', 'telehealth_psychology', " +
                "'recovery_mission', 'plan_adaptation')");
            t.HasCheckConstraint("ck_interventions_status_values",
                "\"status\" IN ('detected', 'evaluated', 'recommended', 'accepted', " +
                "'in_progress', 'completed', 'reevaluation')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.WeaknessId)
            .HasColumnName("weakness_id");

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasMaxLength(60)
            .HasConversion<string>();

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(120);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .HasConversion<string>()
            .HasDefaultValue(InterventionStatus.detected);

        builder.Property(x => x.Severity)
            .HasColumnName("severity")
            .HasMaxLength(20)
            .HasDefaultValue("medium");

        // assigned_to: FK a auth.users por SQL en la migración (fuera del modelo EF),
        // ON DELETE SET NULL (mismo patrón que weaknesses.assigned_to).
        builder.Property(x => x.AssignedTo)
            .HasColumnName("assigned_to");

        builder.Property(x => x.RecommendedAt)
            .HasColumnName("recommended_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.AcceptedAt)
            .HasColumnName("accepted_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.PatientAction)
            .HasColumnName("patient_action")
            .HasMaxLength(120);

        builder.Property(x => x.Result)
            .HasColumnName("result")
            .HasColumnType("text");

        builder.Property(x => x.XpAwardedTotal)
            .HasColumnName("xp_awarded_total")
            .HasDefaultValue(0);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Relationships
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Weakness)
            .WithMany()
            .HasForeignKey(x => x.WeaknessId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes (SPEC §22, A): cola del paciente por estado y cola clínica global
        builder.HasIndex(x => new { x.PatientId, x.Status })
            .HasDatabaseName("ix_interventions_patient_status");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_interventions_status");
    }
}
