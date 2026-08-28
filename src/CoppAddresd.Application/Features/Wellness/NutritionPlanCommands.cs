using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Wellness;

// --- List Nutrition Plans ---

public record ListNutritionPlansQuery(
    bool? IsTemplate = null, string? Search = null, string? Status = null,
    Guid? PatientId = null, int Page = 1, int PageSize = 20)
    : IRequest<PaginatedNutritionPlanResult>;

public sealed class ListNutritionPlansQueryHandler(
    IWellnessRepository repository) : IRequestHandler<ListNutritionPlansQuery, PaginatedNutritionPlanResult>
{
    public async Task<PaginatedNutritionPlanResult> Handle(ListNutritionPlansQuery request, CancellationToken ct)
    {
        var (items, total) = await repository.ListPlansAsync(
            request.IsTemplate, request.Search, request.Status, request.PatientId,
            Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 100), ct);

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
            DurationDays = r.DurationDays,
            DailyCalorieTarget = r.DailyCalorieTarget,
            IsTemplate = r.IsTemplate,
            PatientId = r.PatientId,
            SourcePlanId = r.SourcePlanId,
            Status = r.Status,
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
                Calories = d.Calories,
                Notes = string.IsNullOrWhiteSpace(d.Notes) ? null : d.Notes.Trim(),
                SortOrder = d.SortOrder,
                MediaId = d.MediaId,
                CreatedAt = DateTime.UtcNow,
            }).ToList();
        }

        // Modo personalizado: el plan se asigna al paciente en la misma
        // transacción que su creación (nunca un plan sin asignación).
        NutritionPlanAssignment? assignment = null;

        if (r.PatientId is not null && !r.IsTemplate)
        {
            assignment = new NutritionPlanAssignment
            {
                Id = Guid.NewGuid(),
                PatientId = r.PatientId.Value,
                PlanId = plan.Id,
                StartDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                EndDate = null,
                Status = AssignmentStatus.Active,
                Notes = null,
                CreatedBy = request.CreatedBy,
                CreatedAt = DateTime.UtcNow,
            };

            await repository.AddPlanWithAssignmentAsync(plan, assignment, ct);
        }
        else
        {
            await repository.AddPlanAsync(plan, ct);
        }

        logger.LogInformation("NutritionPlan creado: {Id} ({Name})", plan.Id, plan.Name);

        var created = await repository.GetPlanByIdAsync(plan.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer el plan creado.");

        return NutritionPlanDto.FromEntity(created, assignment?.Id);
    }
}

// --- Update Nutrition Plan ---

public record UpdateNutritionPlanCommand(Guid Id, UpdateNutritionPlanRequest Request)
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
        plan.Name = r.Name.Trim();
        plan.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        plan.TargetCondition = string.IsNullOrWhiteSpace(r.TargetCondition) ? null : r.TargetCondition.Trim();
        plan.DurationDays = r.DurationDays;
        plan.DailyCalorieTarget = r.DailyCalorieTarget;
        plan.Status = r.Status;
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
                Calories = d.Calories,
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
    }
}

// --- Delete Nutrition Plan ---

public record DeleteNutritionPlanCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteNutritionPlanCommandHandler(
    IWellnessRepository repository,
    ILogger<DeleteNutritionPlanCommandHandler> logger) : IRequestHandler<DeleteNutritionPlanCommand, bool>
{
    public async Task<bool> Handle(DeleteNutritionPlanCommand request, CancellationToken ct)
    {
        var plan = await repository.GetPlanByIdAsync(request.Id, ct);
        if (plan is null) return false;

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
            DurationDays = source.DurationDays,
            DailyCalorieTarget = source.DailyCalorieTarget,
            IsTemplate = false,
            PatientId = request.PatientId,
            SourcePlanId = source.Id,
            Status = NutritionPlanStatus.Active,
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
                Calories = d.Calories,
                Notes = d.Notes,
                SortOrder = d.SortOrder,
                MediaId = d.MediaId,
                CreatedAt = DateTime.UtcNow,
            }).ToList(),
        };

        // Modo personalizado: el plan clonado se asigna al paciente en la misma
        // transacción que su creación (nunca un plan sin asignación), igual que
        // el flujo de creación. Sin paciente, se clona solo la plantilla.
        NutritionPlanAssignment? assignment = null;

        if (request.PatientId is { } patientId)
        {
            assignment = new NutritionPlanAssignment
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                PlanId = cloned.Id,
                StartDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                EndDate = null,
                Status = AssignmentStatus.Active,
                Notes = null,
                CreatedBy = request.CreatedBy,
                CreatedAt = DateTime.UtcNow,
            };

            await repository.AddPlanWithAssignmentAsync(cloned, assignment, ct);
        }
        else
        {
            await repository.AddPlanAsync(cloned, ct);
        }

        logger.LogInformation("NutritionPlan clonado: {SourceId} → {ClonedId} (Paciente: {PatientId})",
            source.Id, cloned.Id, request.PatientId);

        var created = await repository.GetPlanByIdAsync(cloned.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer el plan clonado.");

        return NutritionPlanDto.FromEntity(created, assignment?.Id);
    }
}
