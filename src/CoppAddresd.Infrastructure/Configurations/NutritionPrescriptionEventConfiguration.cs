using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

public sealed class NutritionPrescriptionEventConfiguration : IEntityTypeConfiguration<NutritionPrescriptionEvent>
{
    public void Configure(EntityTypeBuilder<NutritionPrescriptionEvent> b)
    {
        b.ToTable("nutrition_prescription_events", "app"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PrescriptionId).HasColumnName("prescription_id");
        b.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        b.Property(x => x.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamptz");
        b.Property(x => x.Action).HasColumnName("action").HasMaxLength(40);
        b.Property(x => x.Revision).HasColumnName("revision");
        b.Property(x => x.Comment).HasColumnName("comment");
        b.Property(x => x.CriticalCount).HasColumnName("critical_count");
        b.Property(x => x.CriticalConfirmed).HasColumnName("critical_confirmed");
        b.Property(x => x.AutoApproved).HasColumnName("auto_approved");
        b.Property(x => x.SnapshotJson).HasColumnName("snapshot_json").HasColumnType("jsonb");
        b.HasOne<NutritionPrescription>().WithMany().HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PrescriptionId, x.OccurredAt }).HasDatabaseName("ix_nutrition_prescription_events_history");
    }
}
