using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>Configuración del libro mayor de XP en el schema <c>app</c>.</summary>
public sealed class XpLedgerEntryConfiguration : IEntityTypeConfiguration<XpLedgerEntry>
{
    public void Configure(EntityTypeBuilder<XpLedgerEntry> builder)
    {
        builder.ToTable("xp_ledger", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.EnrollmentId)
            .HasColumnName("enrollment_id");

        builder.Property(x => x.Amount)
            .HasColumnName("amount");

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(30)
            .HasConversion<string>();

        builder.Property(x => x.SourceRefType)
            .HasColumnName("source_ref_type")
            .HasMaxLength(20);

        builder.Property(x => x.SourceRefId)
            .HasColumnName("source_ref_id");

        // Provenance del catálogo de reglas XP (SPEC §14): FK por código hacia
        // app.xp_rules.code (principal key alterna, respaldada por el índice
        // único uq_xp_rules_code). RESTRICT: una regla ya usada por el libro
        // mayor no se puede borrar; las ediciones son prospective only.
        // La navegación Rule permite que las proyecciones de balance excluyan
        // las filas pendientes de validación (SPEC §15: regla con
        // requires_validation = true y validated_by null no cuenta en los
        // totales hasta aprobarse).
        builder.Property(x => x.RuleCode)
            .HasColumnName("rule_code")
            .HasMaxLength(60);

        builder.HasOne(x => x.Rule)
            .WithMany()
            .HasPrincipalKey(x => x.Code)
            .HasForeignKey(x => x.RuleCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.BalanceAfter)
            .HasColumnName("balance_after");

        builder.Property(x => x.AwardedAt)
            .HasColumnName("awarded_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        // El actor (granted_by) apunta a auth.users; la FK se crea por SQL en
        // la migración (fuera del modelo EF).
        builder.Property(x => x.GrantedBy)
            .HasColumnName("granted_by");

        // Validación profesional del otorgamiento (SPEC §15): se fija cuando
        // un clínico aprueba una XP que requiere validación
        // (CLINICAL_SIGNIFICANT). FK por SQL a auth."Users" (ON DELETE SET
        // NULL) creada en la migración, fuera del modelo EF.
        builder.Property(x => x.ValidatedBy)
            .HasColumnName("validated_by");

        builder.Property(x => x.ValidatedAt)
            .HasColumnName("validated_at")
            .HasColumnType("timestamptz");

        // Multiplicador efectivo aplicado al otorgamiento (SPEC §16, C):
        // regla × paciente (DECIMAL(4,2) nullable; null en filas previas a la
        // columna — prospective only, nunca se reescribe historial).
        builder.Property(x => x.MultiplierUsed)
            .HasColumnName("multiplier_used")
            .HasPrecision(4, 2);

        // Indexes
        builder.HasIndex(x => x.EnrollmentId)
            .HasDatabaseName("ix_xp_ledger_enrollment_id");

        builder.HasIndex(x => new { x.EnrollmentId, x.AwardedAt })
            .HasDatabaseName("ix_xp_ledger_enrollment_awarded_at");

        // Dedupe parcial: una sola entrada por (source_ref_type, source_ref_id, reason)
        // cuando hay referencia de origen (ej: nunca dos DailyBonus por check-in).
        builder.HasIndex(x => new { x.SourceRefType, x.SourceRefId, x.Reason })
            .HasDatabaseName("uq_xp_ledger_source_dedupe")
            .IsUnique()
            .HasFilter("\"source_ref_id\" IS NOT NULL");
    }
}