using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

public sealed class NutritionPlanReviewEventConfiguration : IEntityTypeConfiguration<NutritionPlanReviewEvent>
{
    public void Configure(EntityTypeBuilder<NutritionPlanReviewEvent> b)
    {
        b.ToTable("nutrition_plan_review_events", "app");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PlanId).HasColumnName("plan_id");
        b.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        b.Property(x => x.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamptz");
        b.Property(x => x.Action).HasColumnName("action").HasMaxLength(40);
        b.Property(x => x.Revision).HasColumnName("revision");
        b.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1000);
        b.Property(x => x.Snapshot).HasColumnName("snapshot").HasColumnType("jsonb");
        b.HasIndex(x => new { x.PlanId, x.OccurredAt }).HasDatabaseName("ix_nutrition_plan_review_events_plan_time");
        // El historial conserva el identificador aun si se archiva/elimina el plan.
    }
}
