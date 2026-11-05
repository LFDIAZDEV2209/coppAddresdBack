using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

public sealed class NutritionDraftReservationConfiguration : IEntityTypeConfiguration<NutritionDraftReservation>
{
    public void Configure(EntityTypeBuilder<NutritionDraftReservation> b)
    {
        b.ToTable("nutrition_draft_reservations", "app", table => table.HasCheckConstraint("ck_nutrition_draft_state", "state IN ('pending','generating','ready','failed')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PatientId).HasColumnName("patient_id");
        b.Property(x => x.BatteryAssignmentId).HasColumnName("battery_assignment_id");
        b.Property(x => x.State).HasColumnName("state").HasMaxLength(20);
        b.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(100);
        b.Property(x => x.Attempts).HasColumnName("attempts");
        b.Property(x => x.ClaimToken).HasColumnName("claim_token");
        b.Property(x => x.LeaseUntil).HasColumnName("lease_until");
        b.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.PrescriptionId).HasColumnName("prescription_id");
        b.Property(x => x.Fallback).HasColumnName("fallback");
        b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PatientId, x.BatteryAssignmentId }).IsUnique().HasDatabaseName("uq_nutrition_draft_patient_battery");
        b.HasIndex(x => new { x.State, x.NextAttemptAt }).HasDatabaseName("ix_nutrition_draft_pending");
    }
}
