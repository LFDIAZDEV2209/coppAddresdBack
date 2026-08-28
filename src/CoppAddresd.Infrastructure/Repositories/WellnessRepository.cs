using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class WellnessRepository(AppDbContext dbContext) : IWellnessRepository
{
    // --- Nutrition Plans ---

    public async Task<NutritionPlan?> GetPlanByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.NutritionPlans
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Days.OrderBy(d => d.DayNumber).ThenBy(d => d.SortOrder))
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<(IReadOnlyList<NutritionPlan> Items, int Total)> ListPlansAsync(
        bool? isTemplate, string? search, string? status, Guid? patientId,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext.NutritionPlans
            .AsNoTracking()
            .Include(x => x.Patient)
            .AsQueryable();

        if (patientId.HasValue && !isTemplate.HasValue)
        {
            query = query.Where(x => x.IsTemplate || x.PatientId == patientId.Value);
        }
        else
        {
            if (isTemplate.HasValue)
                query = query.Where(x => x.IsTemplate == isTemplate.Value);

            if (patientId.HasValue)
                query = query.Where(x => x.PatientId == patientId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Name.Contains(search));

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status.ToString() == status);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<NutritionPlan> AddPlanAsync(NutritionPlan plan, CancellationToken ct = default)
    {
        dbContext.NutritionPlans.Add(plan);
        await dbContext.SaveChangesAsync(ct);
        return plan;
    }

    public async Task<NutritionPlanAssignment> AddPlanWithAssignmentAsync(
        NutritionPlan plan,
        NutritionPlanAssignment assignment,
        CancellationToken ct = default)
    {
        // NpgsqlRetryingExecutionStrategy no admite transacciones iniciadas por
        // el usuario fuera de su unidad retriable: se envuelve la operación.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                dbContext.NutritionPlans.Add(plan);
                dbContext.NutritionPlanAssignments.Add(assignment);
                await dbContext.SaveChangesAsync(ct);

                await transaction.CommitAsync(ct);
                return assignment;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task UpdatePlanAsync(NutritionPlan plan, CancellationToken ct = default)
    {
        dbContext.NutritionPlans.Update(plan);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeletePlanAsync(NutritionPlan plan, CancellationToken ct = default)
    {
        dbContext.NutritionPlans.Remove(plan);
        await dbContext.SaveChangesAsync(ct);
    }

    // --- Nutrition Plan Days ---

    public async Task<IReadOnlyList<NutritionPlanDay>> ListPlanDaysAsync(Guid planId, CancellationToken ct = default)
        => await dbContext.NutritionPlanDays
            .AsNoTracking()
            .Where(x => x.PlanId == planId)
            .OrderBy(x => x.DayNumber)
            .ThenBy(x => x.SortOrder)
            .ToListAsync(ct);

    public async Task<NutritionPlanDay?> GetPlanDayByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.NutritionPlanDays
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddPlanDaysRangeAsync(IEnumerable<NutritionPlanDay> days, CancellationToken ct = default)
    {
        dbContext.NutritionPlanDays.AddRange(days);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeletePlanDaysByPlanIdAsync(Guid planId, CancellationToken ct = default)
    {
        var days = await dbContext.NutritionPlanDays
            .Where(x => x.PlanId == planId)
            .ToListAsync(ct);

        dbContext.NutritionPlanDays.RemoveRange(days);
        await dbContext.SaveChangesAsync(ct);
    }

    // --- Exercise Routines ---

    public async Task<ExerciseRoutine?> GetRoutineByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.ExerciseRoutines
            .AsNoTracking()
            .Include(x => x.Exercises.OrderBy(e => e.SortOrder))
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<(IReadOnlyList<ExerciseRoutine> Items, int Total)> ListRoutinesAsync(
        string? search, string? status, string? category,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext.ExerciseRoutines.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Name.Contains(search));

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status.ToString() == status);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category.ToString() == category);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<ExerciseRoutine> AddRoutineAsync(ExerciseRoutine routine, CancellationToken ct = default)
    {
        dbContext.ExerciseRoutines.Add(routine);
        await dbContext.SaveChangesAsync(ct);
        return routine;
    }

    public async Task<RoutineAssignment> AddRoutineWithAssignmentAsync(
        ExerciseRoutine routine,
        RoutineAssignment assignment,
        CancellationToken ct = default)
    {
        // NpgsqlRetryingExecutionStrategy no admite transacciones iniciadas por
        // el usuario fuera de su unidad retriable: se envuelve la operación.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                dbContext.ExerciseRoutines.Add(routine);
                dbContext.RoutineAssignments.Add(assignment);
                await dbContext.SaveChangesAsync(ct);

                await transaction.CommitAsync(ct);
                return assignment;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task UpdateRoutineAsync(ExerciseRoutine routine, CancellationToken ct = default)
    {
        dbContext.ExerciseRoutines.Update(routine);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteRoutineAsync(ExerciseRoutine routine, CancellationToken ct = default)
    {
        dbContext.ExerciseRoutines.Remove(routine);
        await dbContext.SaveChangesAsync(ct);
    }

    // --- Routine Exercises ---

    public async Task<IReadOnlyList<RoutineExercise>> ListRoutineExercisesAsync(Guid routineId, CancellationToken ct = default)
        => await dbContext.RoutineExercises
            .AsNoTracking()
            .Where(x => x.RoutineId == routineId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

    public async Task<RoutineExercise?> GetRoutineExerciseByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.RoutineExercises
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddRoutineExercisesRangeAsync(IEnumerable<RoutineExercise> exercises, CancellationToken ct = default)
    {
        dbContext.RoutineExercises.AddRange(exercises);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteRoutineExercisesByRoutineIdAsync(Guid routineId, CancellationToken ct = default)
    {
        var exercises = await dbContext.RoutineExercises
            .Where(x => x.RoutineId == routineId)
            .ToListAsync(ct);

        dbContext.RoutineExercises.RemoveRange(exercises);
        await dbContext.SaveChangesAsync(ct);
    }

    // --- Routine Assignments ---

    public async Task<RoutineAssignment?> GetAssignmentByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.RoutineAssignments
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Routine)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<(IReadOnlyList<RoutineAssignment> Items, int Total)> ListAssignmentsAsync(
        Guid? patientId, Guid? routineId, string? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext.RoutineAssignments
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Routine)
            .AsQueryable();

        if (patientId.HasValue)
            query = query.Where(x => x.PatientId == patientId.Value);

        if (routineId.HasValue)
            query = query.Where(x => x.RoutineId == routineId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status.ToString() == status);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<RoutineAssignment>> ListAssignmentsByPatientAsync(Guid patientId, CancellationToken ct = default)
        => await dbContext.RoutineAssignments
            .AsNoTracking()
            .Include(x => x.Routine)
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<RoutineAssignment> AddAssignmentAsync(RoutineAssignment assignment, CancellationToken ct = default)
    {
        dbContext.RoutineAssignments.Add(assignment);
        await dbContext.SaveChangesAsync(ct);
        return assignment;
    }

    public async Task UpdateAssignmentAsync(RoutineAssignment assignment, CancellationToken ct = default)
    {
        dbContext.RoutineAssignments.Update(assignment);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAssignmentAsync(RoutineAssignment assignment, CancellationToken ct = default)
    {
        dbContext.RoutineAssignments.Remove(assignment);
        await dbContext.SaveChangesAsync(ct);
    }

    // --- Nutrition Plan Assignments ---

    public async Task<NutritionPlanAssignment?> GetPlanAssignmentByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.NutritionPlanAssignments
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<(IReadOnlyList<NutritionPlanAssignment> Items, int Total)> ListPlanAssignmentsAsync(
        Guid? patientId, Guid? planId, string? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext.NutritionPlanAssignments
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Plan)
            .AsQueryable();

        if (patientId.HasValue)
            query = query.Where(x => x.PatientId == patientId.Value);

        if (planId.HasValue)
            query = query.Where(x => x.PlanId == planId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status.ToString() == status);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<NutritionPlanAssignment>> ListPlanAssignmentsByPatientAsync(Guid patientId, CancellationToken ct = default)
        => await dbContext.NutritionPlanAssignments
            .AsNoTracking()
            .Include(x => x.Plan)
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<NutritionPlanAssignment> AddPlanAssignmentAsync(NutritionPlanAssignment assignment, CancellationToken ct = default)
    {
        dbContext.NutritionPlanAssignments.Add(assignment);
        await dbContext.SaveChangesAsync(ct);
        return assignment;
    }

    public async Task UpdatePlanAssignmentAsync(NutritionPlanAssignment assignment, CancellationToken ct = default)
    {
        dbContext.NutritionPlanAssignments.Update(assignment);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeletePlanAssignmentAsync(NutritionPlanAssignment assignment, CancellationToken ct = default)
    {
        dbContext.NutritionPlanAssignments.Remove(assignment);
        await dbContext.SaveChangesAsync(ct);
    }
}
