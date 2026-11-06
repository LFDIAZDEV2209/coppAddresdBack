namespace CoppAddresd.Domain.Entities;

/// <summary>Versión del ciclo clínico; la publicación nueva se implementa en el grupo 4.</summary>
public sealed class NutritionPrescription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid CycleId { get; set; } = Guid.NewGuid();
    public int Version { get; set; } = 1;
    public int Revision { get; set; } = 1;
    public string State { get; set; } = "borrador_ia";
    public bool Provisional { get; set; }
    public bool IsPublished { get; set; }
    public bool MenusOutdated { get; set; }
    public bool ManualEditsConfirmed { get; set; }
    public string ClinicalProfileJson { get; set; } = "{}";
    public string TargetsJson { get; set; } = "{}";
    public string MissingDataJson { get; set; } = "[]";
    public string NarrativeSource { get; set; } = "deterministic_fallback";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public double? CalorieTarget { get; set; }
    public double? CarbsTarget { get; set; }
    public double? ProteinTarget { get; set; }
    public double? FatTarget { get; set; }
    public double? FiberTarget { get; set; }
    public int? WaterMl { get; set; }
    public string? Instructions { get; set; }
    public string? InstructionsEn { get; set; }
    public string? InstructionsEnSourceHash { get; set; }
    public string? ReturnComment { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public PatientProfile? Patient { get; set; }
    public ICollection<NutritionPrescriptionMeal> Meals { get; set; } = [];
}
