using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>
/// Configuración de las debilidades del paciente en el schema <c>app</c>
/// (SPEC §21, "Paso 7c"): hallazgos del motor determinista de reglas que el
/// clínico prioriza en su cola. Índices de lectura de la cola del paciente
/// <c>(patient_id, status)</c> y de la cola clínica global <c>(status)</c>;
/// la dedupe de detección (AC-43) lee por <c>(patient_id, status, code)</c>
/// sobre el primer índice.
///
/// La columna <c>assigned_to</c> apunta a <c>auth.users</c>; la FK se crea por
/// SQL en la migración (patrón <c>user_id</c>, fuera del modelo EF), ON DELETE
/// SET NULL.
/// </summary>
public sealed class WeaknessConfiguration : IEntityTypeConfiguration<Weakness>
{
    public void Configure(EntityTypeBuilder<Weakness> builder)
    {
        builder.ToTable("weaknesses", "app", t =>
        {
            t.HasCheckConstraint("ck_weaknesses_category_values",
                "\"category\" IN ('nutritional', 'clinical', 'psychological', 'exercise', 'adherence', 'supplement', 'sleep', 'motivation')");
            t.HasCheckConstraint("ck_weaknesses_severity_values",
                "\"severity\" IN ('low', 'medium', 'high', 'critical')");
            t.HasCheckConstraint("ck_weaknesses_status_values",
                "\"status\" IN ('open', 'acknowledged', 'in_intervention', 'resolved', 'dismissed')");
            t.HasCheckConstraint("ck_weaknesses_source_values",
                "\"source\" IN ('ai', 'professional', 'system')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(60);

        builder.Property(x => x.Category)
            .HasColumnName("category")
            .HasMaxLength(40)
            .HasConversion<string>();

        builder.Property(x => x.Severity)
            .HasColumnName("severity")
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(WeaknessSeverity.low);

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(120);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(x => x.DetectedAt)
            .HasColumnName("detected_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.MetricId)
            .HasColumnName("metric_id");

        builder.Property(x => x.IndicatorValue)
            .HasColumnName("indicator_value")
            .HasPrecision(10, 4);

        builder.Property(x => x.Source)
            .HasColumnName("source")
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(WeaknessSource.ai);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(WeaknessStatus.open);

        // El actor (assigned_to) apunta a auth.users; la FK se crea por SQL en
        // la migración (fuera del modelo EF), ON DELETE SET NULL.
        builder.Property(x => x.AssignedTo)
            .HasColumnName("assigned_to");

        builder.Property(x => x.ResolvedAt)
            .HasColumnName("resolved_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Relationships: el paciente dueño y la métrica opcional del indicador.
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Metric)
            .WithMany()
            .HasForeignKey(x => x.MetricId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes (SPEC §21, A): cola del paciente por estado y cola clínica
        // global de debilidades abiertas; la dedupe AC-43 usa (patient, status)
        // + code en memoria (el catálogo de reglas es pequeño y estable).
        builder.HasIndex(x => new { x.PatientId, x.Status })
            .HasDatabaseName("ix_weaknesses_patient_status");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_weaknesses_status");
    }
}