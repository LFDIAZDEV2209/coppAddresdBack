using CoppAddresd.Application.DTOs.ProgramProgress;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    [NutritionPostgresFact]
    public async Task PublishedMission_PreservesClinicalNotesAndNutrition_WithoutShowingInternalGenerationStatus()
    {
        await using var db = Context();
        var seed = await MissionSeedAsync(db, false);
        const string original = "Propuesta determinista pendiente de revisión.\nRevisar porciones y alertas antes de aprobar.\nNo añadir azúcar; mantener 50 g de arroz.";
        seed.Prescription.Instructions = original;
        foreach (var meal in seed.Prescription.Meals.Where(meal => meal.DayNumber == 30))
            meal.Notes = meal.MealType == "mer" ? "Mantener la porción de 20 g de nueces." : null;
        await db.SaveChangesAsync();

        var snapshot = await MissionRepository(db).GetSnapshotAsync(seed.Enrollment, MissionToday, CancellationToken.None);
        var content = Assert.IsType<TodayTaskContentDto>(Assert.Single(snapshot!.TodayTasks).Content);
        var meals = content.NutritionMeals!;
        Assert.Equal(4, meals.Count);
        Assert.All(meals, meal =>
        {
            Assert.Equal(meal.MealType == "Snack" ? "Mantener la porción de 20 g de nueces." : "No añadir azúcar; mantener 50 g de arroz.", meal.Notes);
            Assert.Equal("Alimento de prueba x 100 g", meal.Foods);
            Assert.Equal(165, meal.Calories); Assert.Equal(10m, meal.ProteinG);
            Assert.Equal(20m, meal.CarbsG); Assert.Equal(5m, meal.FatG);
        });
        var stored = await db.NutritionPrescriptions.AsNoTracking().SingleAsync(plan => plan.Id == seed.Prescription.Id);
        Assert.Equal(original, stored.Instructions);
        Assert.True(stored.IsPublished); Assert.Equal("aprobado", stored.State);
        Assert.Equal(3, await db.NutritionPrescriptionMeals.CountAsync(meal => meal.PrescriptionId == stored.Id && meal.DayNumber == 30 && meal.Notes == null));
    }
}
