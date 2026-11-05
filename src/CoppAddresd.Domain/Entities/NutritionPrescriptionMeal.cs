namespace CoppAddresd.Domain.Entities;

/// <summary>Comida del ciclo (día 1–30; desayuno, almuerzo, merienda o cena).</summary>
public sealed class NutritionPrescriptionMeal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PrescriptionId { get; set; }
    public int DayNumber { get; set; }
    public string MealType { get; set; } = "";
    public TimeOnly ScheduledTime { get; set; }
    public string? MenuName { get; set; }
    public string? Notes { get; set; }
    public int? WaterMl { get; set; }
    public NutritionPrescription? Prescription { get; set; }
    public ICollection<NutritionPrescriptionItem> Items { get; set; } = [];
}
