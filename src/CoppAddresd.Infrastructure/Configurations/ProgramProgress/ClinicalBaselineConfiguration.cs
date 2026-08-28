using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>
/// Configuración de <c>app.clinical_baselines</c> (SPEC §13.1.2): línea base
/// clínica por <c>(patient_id, metric_id)</c>, con FKs RESTRICT hacia
/// <c>app.patient_profiles</c>, <c>app.measurement_metrics</c> y
/// <c>app.unit_of_measures</c>. El <c>set_by</c> apunta a <c>auth.users</c> y
/// es NOT NULL: la FK se crea por SQL en la migración con <c>ON DELETE
/// RESTRICT</c> (a diferencia de los actores nullable SET NULL, RESTRICT es la
/// única opción válida para una columna NOT NULL de autoría clínica).
/// </summary>
public sealed class ClinicalBaselineConfiguration : IEntityTypeConfiguration<ClinicalBaseline>
{
    public void Configure(EntityTypeBuilder<ClinicalBaseline> builder)
    {
        builder.ToTable("clinical_baselines", "app", t =>
        {
            t.HasCheckConstraint(
                "ck_clinical_baselines_favorable_direction",
                "\"favorable_direction\" IN (-1, 1)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PatientId)
            .HasColumnName("patient_id");

        builder.Property(x => x.MetricId)
            .HasColumnName("metric_id");

        builder.Property(x => x.Value)
            .HasColumnName("value")
            .HasPrecision(10, 4);

        builder.Property(x => x.UnitId)
            .HasColumnName("unit_id");

        // favorable_direction: enum con valores -1/1 persistido como smallint
        // (conversión numérica por defecto de EF; CHECK IN (-1,1) arriba).
        builder.Property(x => x.FavorableDirection)
            .HasColumnName("favorable_direction")
            .HasColumnType("smallint");

        builder.Property(x => x.TargetValue)
            .HasColumnName("target_value")
            .HasPrecision(10, 4);

        builder.Property(x => x.MeasuredAt)
            .HasColumnName("measured_at")
            .HasColumnType("date");

        // set_by: FK a auth.users creada por SQL en la migración (NOT NULL,
        // sin navegación EF; AC-22 exige set_by clínico).
        builder.Property(x => x.SetBy)
            .HasColumnName("set_by");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Indexes / constraints (SPEC §13.1.2)
        builder.HasIndex(x => new { x.PatientId, x.MetricId })
            .HasDatabaseName("uq_clinical_baselines_patient_metric")
            .IsUnique();

        builder.HasIndex(x => x.PatientId)
            .HasDatabaseName("ix_clinical_baselines_patient_id");

        builder.HasIndex(x => x.MetricId)
            .HasDatabaseName("ix_clinical_baselines_metric_id");

        builder.HasIndex(x => x.SetBy)
            .HasDatabaseName("ix_clinical_baselines_set_by");

        // Relationships (FKs EF a tablas del catálogo clínico, todas RESTRICT)
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Metric)
            .WithMany()
            .HasForeignKey(x => x.MetricId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Unit)
            .WithMany()
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}