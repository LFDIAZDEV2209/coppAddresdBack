using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>Persistencia atómica del agregado; cada guardado incluye comidas e ítems.</summary>
public sealed class NutritionPrescriptionRepository(AppDbContext db) : INutritionPrescriptionRepository
{
    public async Task<IReadOnlyList<NutritionFoodItem>> ListFoodsAsync(CancellationToken ct)
        => await db.NutritionFoodItems.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);

    public async Task<NutritionPrescription?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.NutritionPrescriptions.AsNoTracking()
            .Include(x => x.Meals.OrderBy(m => m.DayNumber).ThenBy(m => m.ScheduledTime))
            .ThenInclude(x => x.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
            .ThenInclude(x => x.FoodItem).AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<NutritionPrescription>> ListByPatientAsync(Guid patientId, int page, int pageSize, CancellationToken ct)
        => await db.NutritionPrescriptions.AsNoTracking().Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.StartDate).ThenByDescending(x => x.Id)
            .Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100))
            .Take(Math.Clamp(pageSize, 1, 100)).ToListAsync(ct);

    public async Task<NutritionPrescription?> GetCurrentPublishedAsync(Guid patientId, DateOnly today, CancellationToken ct)
        => await db.NutritionPrescriptions.AsNoTracking()
            .Where(x => x.PatientId == patientId && x.IsPublished && x.State == "aprobado"
                && x.StartDate <= today && x.EndDate >= today && x.StartDate > today.AddDays(-30))
            .OrderByDescending(x => x.StartDate).ThenByDescending(x => x.Id)
            .Include(x => x.Meals.OrderBy(m => m.DayNumber).ThenBy(m => m.ScheduledTime))
            .ThenInclude(x => x.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
            .ThenInclude(x => x.FoodItem).AsSplitQuery().FirstOrDefaultAsync(ct);

    public async Task AddAsync(NutritionPrescription prescription, CancellationToken ct)
    {
        db.NutritionPrescriptions.Add(prescription);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(NutritionPrescription prescription, CancellationToken ct)
    {
        // Se reconcilia el grafo completo y se persiste en un solo SaveChanges.
        // Así las eliminaciones, altas y cambios de revisión son atómicos.
        var stored = await db.NutritionPrescriptions.Include(x => x.Meals).ThenInclude(x => x.Items)
            .AsSplitQuery().SingleAsync(x => x.Id == prescription.Id, ct);
        db.Entry(stored).CurrentValues.SetValues(prescription);
        db.Entry(stored).Property(x => x.Revision).OriginalValue = prescription.Revision;
        stored.Revision = prescription.Revision + 1;
        foreach (var removed in stored.Meals.Where(x => !prescription.Meals.Any(m => m.Id == x.Id)).ToList())
            db.NutritionPrescriptionMeals.Remove(removed);
        foreach (var meal in prescription.Meals)
        {
            var existing = stored.Meals.SingleOrDefault(x => x.Id == meal.Id);
            if (existing is null)
            {
                meal.PrescriptionId = stored.Id;
                meal.Prescription = null;
                foreach (var item in meal.Items) item.FoodItem = null;
                db.NutritionPrescriptionMeals.Add(meal);
                continue;
            }
            db.Entry(existing).CurrentValues.SetValues(meal);
            foreach (var removed in existing.Items.Where(x => !meal.Items.Any(i => i.Id == x.Id)).ToList())
                db.NutritionPrescriptionItems.Remove(removed);
            foreach (var item in meal.Items)
            {
                var saved = existing.Items.SingleOrDefault(x => x.Id == item.Id);
                if (saved is not null) db.Entry(saved).CurrentValues.SetValues(item);
                else
                {
                    item.MealId = existing.Id;
                    item.Meal = null;
                    item.FoodItem = null;
                    db.NutritionPrescriptionItems.Add(item);
                }
            }
        }
        await db.SaveChangesAsync(ct);
        prescription.Revision = stored.Revision;
    }
}
