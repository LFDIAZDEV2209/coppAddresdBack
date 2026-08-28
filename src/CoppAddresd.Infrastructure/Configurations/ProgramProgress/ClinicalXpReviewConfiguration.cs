using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>
/// Configuración de las revisiones clínicas de XP en el schema <c>app</c>
/// (SPEC §15): cola de mejorías significativas que un clínico debe decidir
/// antes de otorgar <c>CLINICAL_SIGNIFICANT</c>. Una fila por
/// <c>(patient_id, health_score_id, metric_id)</c> (período × métrica).
/// </summary>
public sealed class ClinicalXpReviewConfiguration : IEntityTypeConfiguration<ClinicalXpReview>
{
    public void Configure(EntityTypeBuilder<ClinicalXpReview> builder)
    {
        builder.ToTable("clinical_xp_reviews", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.HealthScoreId)
            .HasColumnName("health_score_id");

        builder.Property(x => x.MetricId)
            .HasColumnName("metric_id");

        // rule_code: regla que se otorga si la revisión se aprueba. Se guarda
        // como string (varchar(60), default CLINICAL_SIGNIFICANT): la regla
        // vive en app.xp_rules y el código es la llave de negocio (provenance).
        builder.Property(x => x.RuleCode)
            .HasColumnName("rule_code")
            .HasMaxLength(60)
            .HasDefaultValue(XpRuleCodes.ClinicalSignificant);

        builder.Property(x => x.DeltaPct)
            .HasColumnName("delta_pct")
            .HasPrecision(8, 3);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(ClinicalXpReviewStatus.pending);

        // El actor (decided_by) apunta a auth.users; la FK se crea por SQL en
        // la migración (fuera del modelo EF), ON DELETE SET NULL.
        builder.Property(x => x.DecidedBy)
            .HasColumnName("decided_by");

        builder.Property(x => x.DecidedAt)
            .HasColumnName("decided_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // FKs a tablas del módulo y catálogos existentes.
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HealthScore)
            .WithMany()
            .HasForeignKey(x => x.HealthScoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Metric)
            .WithMany()
            .HasForeignKey(x => x.MetricId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes (SPEC §15): una revisión por (paciente, período, métrica);
        // cola de pendientes por status; trazabilidad por decidido.
        builder.HasIndex(x => new { x.PatientId, x.HealthScoreId, x.MetricId })
            .HasDatabaseName("uq_clinical_xp_reviews_patient_score_metric")
            .IsUnique();

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_clinical_xp_reviews_status");

        builder.HasIndex(x => x.DecidedBy)
            .HasDatabaseName("ix_clinical_xp_reviews_decided_by");
    }
}