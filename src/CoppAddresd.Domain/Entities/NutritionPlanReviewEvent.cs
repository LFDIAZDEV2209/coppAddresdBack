namespace CoppAddresd.Domain.Entities;

/// <summary>Historial inmutable del contenido y las decisiones clínicas del plan.</summary>
public sealed class NutritionPlanReviewEvent
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public Guid? ActorUserId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Action { get; set; } = default!;
    public int Revision { get; set; }
    public string? Notes { get; set; }
    public string Snapshot { get; set; } = "{}";
}
