using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Wellness;

// --- List Nutrition Plans ---

public record ListNutritionPlansQuery(
    bool? IsTemplate = null, string? Search = null, string? Status = null,
    Guid? PatientId = null, int Page = 1, int PageSize = 20, Guid? ProfessionalId = null)
    : IRequest<PaginatedNutritionPlanResult>;

public sealed class ListNutritionPlansQueryHandler(
    IWellnessRepository repository) : IRequestHandler<ListNutritionPlansQuery, PaginatedNutritionPlanResult>
{
    public async Task<PaginatedNutritionPlanResult> Handle(ListNutritionPlansQuery request, CancellationToken ct)
    {
        var (items, total) = await repository.ListPlansAsync(
            request.IsTemplate, request.Search, request.Status, request.PatientId,
            Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 100), request.ProfessionalId, ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)Math.Clamp(request.PageSize, 1, 100)));

        return new PaginatedNutritionPlanResult(
            items.Select(NutritionPlanListItemDto.FromEntity).ToList(),
            total, Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 100), totalPages);
    }
}

// --- Get Nutrition Plan ---

public record GetNutritionPlanQuery(Guid Id) : IRequest<NutritionPlanDto?>;

public sealed class GetNutritionPlanQueryHandler(
    IWellnessRepository repository) : IRequestHandler<GetNutritionPlanQuery, NutritionPlanDto?>
{
    public async Task<NutritionPlanDto?> Handle(GetNutritionPlanQuery request, CancellationToken ct)
    {
        var plan = await repository.GetPlanByIdAsync(request.Id, ct);
        return plan is null ? null : NutritionPlanDto.FromEntity(plan);
    }
}

// --- Create Nutrition Plan ---

public record CreateNutritionPlanCommand(CreateNutritionPlanRequest Request, Guid? CreatedBy = null)
    : IRequest<NutritionPlanDto>;

public sealed class CreateNutritionPlanCommandHandler(
    IWellnessRepository repository,
    ILogger<CreateNutritionPlanCommandHandler> logger) : IRequestHandler<CreateNutritionPlanCommand, NutritionPlanDto>
{
    public async Task<NutritionPlanDto> Handle(CreateNutritionPlanCommand request, CancellationToken ct)
    {
        var r = request.Request;

        var plan = new NutritionPlan
        {
            Id = Guid.NewGuid(),
            Name = r.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim(),
            TargetCondition = string.IsNullOrWhiteSpace(r.TargetCondition) ? null : r.TargetCondition.Trim(),
            DurationDays = r.IsTemplate ? r.DurationDays : 30,
            DailyCalorieTarget = r.DailyCalorieTarget,
            DailyProteinTarget = r.DailyProteinTarget, DailyCarbsTarget = r.DailyCarbsTarget,
            DailyFatTarget = r.DailyFatTarget, DailyFiberTarget = r.DailyFiberTarget,
            Allergens = r.Allergens, MealTiming = r.MealTiming,
            IsTemplate = r.IsTemplate,
            PatientId = r.IsTemplate ? null : r.PatientId,
            SourcePlanId = r.SourcePlanId,
            Status = r.IsTemplate ? r.Status : NutritionPlanStatus.Draft,
            CreatedBy = request.CreatedBy,
            CreatedAt = DateTime.UtcNow,
        };

        if (r.Days is { Count: > 0 })
        {
            plan.Days = r.Days.Select(d => new NutritionPlanDay
            {
                Id = Guid.NewGuid(),
                DayNumber = d.DayNumber,
                DailyWaterMl = d.DailyWaterMl > 0 ? d.DailyWaterMl : 2000,
                MealType = d.MealType,
                Description = string.IsNullOrWhiteSpace(d.Description) ? null : d.Description.Trim(),
                Foods = string.IsNullOrWhiteSpace(d.Foods) ? null : d.Foods.Trim(),
                Calories = d.Calories, ProteinG = d.ProteinG, CarbsG = d.CarbsG,
                FatG = d.FatG, FiberG = d.FiberG, WaterMl = d.WaterMl,
                Notes = string.IsNullOrWhiteSpace(d.Notes) ? null : d.Notes.Trim(),
                SortOrder = d.SortOrder,
                MediaId = d.MediaId,
                CreatedAt = DateTime.UtcNow,
            }).ToList();
        }

        // El borrador nunca crea una asignación: solo la aprobación clínica publica el ciclo.
        await repository.AddPlanAsync(plan, ct);

        logger.LogInformation("NutritionPlan creado: {Id} ({Name})", plan.Id, plan.Name);

        var created = await repository.GetPlanByIdAsync(plan.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer el plan creado.");

        return NutritionPlanDto.FromEntity(created);
    }
}

// --- Update Nutrition Plan ---

public record UpdateNutritionPlanCommand(Guid Id, UpdateNutritionPlanRequest Request, Guid? UpdatedBy = null)
    : IRequest<NutritionPlanDto?>;

public sealed class UpdateNutritionPlanCommandHandler(
    IWellnessRepository repository,
    ILogger<UpdateNutritionPlanCommandHandler> logger) : IRequestHandler<UpdateNutritionPlanCommand, NutritionPlanDto?>
{
    public async Task<NutritionPlanDto?> Handle(UpdateNutritionPlanCommand request, CancellationToken ct)
    {
        var plan = await repository.GetPlanByIdAsync(request.Id, ct);
        if (plan is null) return null;

        var r = request.Request;
        if (!plan.IsTemplate && (r.Revision is null || r.Revision != plan.Revision))
            throw new CoppAddresd.Domain.Exceptions.BusinessRuleViolationException("El plan cambió. Actualiza y revisa su última versión.");
        if (plan.GenerationStatus is "queued" or "generating")
            throw new CoppAddresd.Domain.Exceptions.BusinessRuleViolationException("Espera a que termine la generación del borrador.");
        return await repository.ExecuteInTransactionAsync(async transactionCt =>
        {
        plan.UpdatedBy = request.UpdatedBy;
        plan.Revision++;
        plan.ReviewStatus = "pending_review";
        plan.ApprovedAt = null;
        plan.ApprovedBy = null;
        if (plan.GenerationStatus == "failed") plan.GenerationStatus = null;
        plan.Name = r.Name.Trim();
        plan.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        plan.TargetCondition = string.IsNullOrWhiteSpace(r.TargetCondition) ? null : r.TargetCondition.Trim();
        plan.DurationDays = plan.IsTemplate ? r.DurationDays : 30;
        plan.DailyCalorieTarget = r.DailyCalorieTarget;
        plan.DailyProteinTarget = r.DailyProteinTarget; plan.DailyCarbsTarget = r.DailyCarbsTarget;
        plan.DailyFatTarget = r.DailyFatTarget; plan.DailyFiberTarget = r.DailyFiberTarget;
        plan.Allergens = r.Allergens; plan.MealTiming = r.MealTiming;
        plan.Status = plan.IsTemplate ? r.Status : NutritionPlanStatus.Draft;
        plan.UpdatedAt = DateTime.UtcNow;

        // Reemplazar días si se proporcionan
        if (r.Days is not null)
        {
            await repository.DeletePlanDaysByPlanIdAsync(plan.Id, ct);

            plan.Days = r.Days.Select(d => new NutritionPlanDay
            {
                Id = Guid.NewGuid(),
                PlanId = plan.Id,
                DayNumber = d.DayNumber,
                DailyWaterMl = d.DailyWaterMl > 0 ? d.DailyWaterMl : 2000,
                MealType = d.MealType,
                Description = string.IsNullOrWhiteSpace(d.Description) ? null : d.Description.Trim(),
                Foods = string.IsNullOrWhiteSpace(d.Foods) ? null : d.Foods.Trim(),
                Calories = d.Calories, ProteinG = d.ProteinG, CarbsG = d.CarbsG,
                FatG = d.FatG, FiberG = d.FiberG, WaterMl = d.WaterMl,
                Notes = string.IsNullOrWhiteSpace(d.Notes) ? null : d.Notes.Trim(),
                SortOrder = d.SortOrder,
                MediaId = d.MediaId,
                CreatedAt = DateTime.UtcNow,
            }).ToList();

            await repository.AddPlanDaysRangeAsync(plan.Days, ct);
        }

        await repository.UpdatePlanAsync(plan, ct);

        logger.LogInformation("NutritionPlan actualizado: {Id}", plan.Id);

        var updated = await repository.GetPlanByIdAsync(plan.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer el plan actualizado.");

        return NutritionPlanDto.FromEntity(updated);
        }, ct);
    }
}

// --- Delete Nutrition Plan ---

public record DeleteNutritionPlanCommand(Guid Id, Guid? DeletedBy = null) : IRequest<bool>;

public sealed class DeleteNutritionPlanCommandHandler(
    IWellnessRepository repository,
    ILogger<DeleteNutritionPlanCommandHandler> logger) : IRequestHandler<DeleteNutritionPlanCommand, bool>
{
    public async Task<bool> Handle(DeleteNutritionPlanCommand request, CancellationToken ct)
    {
        var plan = await repository.GetPlanByIdAsync(request.Id, ct);
        if (plan is null) return false;

        plan.UpdatedBy = request.DeletedBy;
        await repository.DeletePlanAsync(plan, ct);

        logger.LogInformation("NutritionPlan eliminado: {Id}", request.Id);
        return true;
    }
}

// --- Clone Nutrition Plan (template → patient) ---

public record CloneNutritionPlanCommand(Guid SourcePlanId, Guid? PatientId, Guid? CreatedBy = null)
    : IRequest<NutritionPlanDto?>;

public sealed class CloneNutritionPlanCommandHandler(
    IWellnessRepository repository,
    ILogger<CloneNutritionPlanCommandHandler> logger) : IRequestHandler<CloneNutritionPlanCommand, NutritionPlanDto?>
{
    public async Task<NutritionPlanDto?> Handle(CloneNutritionPlanCommand request, CancellationToken ct)
    {
        var source = await repository.GetPlanByIdAsync(request.SourcePlanId, ct);
        if (source is null) return null;

        var cloned = new NutritionPlan
        {
            Id = Guid.NewGuid(),
            Name = source.Name,
            Description = source.Description,
            TargetCondition = source.TargetCondition,
            DurationDays = request.PatientId.HasValue ? 30 : source.DurationDays,
            DailyCalorieTarget = source.DailyCalorieTarget,
            DailyProteinTarget = source.DailyProteinTarget, DailyCarbsTarget = source.DailyCarbsTarget,
            DailyFatTarget = source.DailyFatTarget, DailyFiberTarget = source.DailyFiberTarget,
            Allergens = source.Allergens, MealTiming = source.MealTiming,
            IsTemplate = false,
            PatientId = request.PatientId,
            SourcePlanId = source.Id,
            Status = NutritionPlanStatus.Draft,
            CreatedBy = request.CreatedBy,
            CreatedAt = DateTime.UtcNow,
            Days = source.Days.Select(d => new NutritionPlanDay
            {
                Id = Guid.NewGuid(),
                DayNumber = d.DayNumber,
                DailyWaterMl = d.DailyWaterMl,
                MealType = d.MealType,
                Description = d.Description,
                Foods = d.Foods,
                Calories = d.Calories, ProteinG = d.ProteinG, CarbsG = d.CarbsG,
                FatG = d.FatG, FiberG = d.FiberG, WaterMl = d.WaterMl,
                Notes = d.Notes,
                SortOrder = d.SortOrder,
                MediaId = d.MediaId,
                CreatedAt = DateTime.UtcNow,
            }).ToList(),
        };

        await repository.AddPlanAsync(cloned, ct);

        logger.LogInformation("NutritionPlan clonado: {SourceId} → {ClonedId} (Paciente: {PatientId})",
            source.Id, cloned.Id, request.PatientId);

        var created = await repository.GetPlanByIdAsync(cloned.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer el plan clonado.");

        return NutritionPlanDto.FromEntity(created);
    }
}
