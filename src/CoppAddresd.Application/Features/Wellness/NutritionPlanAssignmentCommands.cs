using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Wellness;

// --- List Nutrition Plan Assignments ---

public record ListNutritionPlanAssignmentsQuery(
    Guid? PatientId = null, Guid? PlanId = null, string? Status = null,
    int Page = 1, int PageSize = 20, Guid? ProfessionalId = null)
    : IRequest<PaginatedNutritionPlanAssignmentResult>;

public sealed class ListNutritionPlanAssignmentsQueryHandler(
    IWellnessRepository repository) : IRequestHandler<ListNutritionPlanAssignmentsQuery, PaginatedNutritionPlanAssignmentResult>
{
    public async Task<PaginatedNutritionPlanAssignmentResult> Handle(ListNutritionPlanAssignmentsQuery request, CancellationToken ct)
    {
        var (items, total) = await repository.ListPlanAssignmentsAsync(
            request.PatientId, request.PlanId, request.Status,
            Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 100), request.ProfessionalId, ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)Math.Clamp(request.PageSize, 1, 100)));

        return new PaginatedNutritionPlanAssignmentResult(
            items.Select(NutritionPlanAssignmentDto.FromEntity).ToList(),
            total, Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 100), totalPages);
    }
}

// --- Get Nutrition Plan Assignment ---

public record GetNutritionPlanAssignmentQuery(Guid Id) : IRequest<NutritionPlanAssignmentDto?>;

public sealed class GetNutritionPlanAssignmentQueryHandler(
    IWellnessRepository repository) : IRequestHandler<GetNutritionPlanAssignmentQuery, NutritionPlanAssignmentDto?>
{
    public async Task<NutritionPlanAssignmentDto?> Handle(GetNutritionPlanAssignmentQuery request, CancellationToken ct)
    {
        var assignment = await repository.GetPlanAssignmentByIdAsync(request.Id, ct);
        return assignment is null ? null : NutritionPlanAssignmentDto.FromEntity(assignment);
    }
}

// --- List Assignments by Patient ---

public record ListPlanAssignmentsByPatientQuery(Guid PatientId) : IRequest<IReadOnlyList<NutritionPlanAssignmentDto>>;

public sealed class ListPlanAssignmentsByPatientQueryHandler(
    IWellnessRepository repository) : IRequestHandler<ListPlanAssignmentsByPatientQuery, IReadOnlyList<NutritionPlanAssignmentDto>>
{
    public async Task<IReadOnlyList<NutritionPlanAssignmentDto>> Handle(ListPlanAssignmentsByPatientQuery request, CancellationToken ct)
    {
        var items = await repository.ListPlanAssignmentsByPatientAsync(request.PatientId, ct);
        return items.Select(NutritionPlanAssignmentDto.FromEntity).ToList();
    }
}

// --- Create Nutrition Plan Assignment ---

public record CreateNutritionPlanAssignmentCommand(CreateNutritionPlanAssignmentRequest Request, Guid? CreatedBy = null)
    : IRequest<NutritionPlanAssignmentDto>;

public sealed class CreateNutritionPlanAssignmentCommandHandler
    : IRequestHandler<CreateNutritionPlanAssignmentCommand, NutritionPlanAssignmentDto>
{
    public Task<NutritionPlanAssignmentDto> Handle(CreateNutritionPlanAssignmentCommand request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        throw new CoppAddresd.Domain.Exceptions.UnprocessableEntityException(
            "Los ciclos nutricionales se asignan únicamente mediante la aprobación clínica del plan.");
    }
}

// --- Update Nutrition Plan Assignment ---

public record UpdateNutritionPlanAssignmentCommand(Guid Id, UpdateNutritionPlanAssignmentRequest Request)
    : IRequest<NutritionPlanAssignmentDto?>;

public sealed class UpdateNutritionPlanAssignmentCommandHandler(
    IWellnessRepository repository,
    ILogger<UpdateNutritionPlanAssignmentCommandHandler> logger) : IRequestHandler<UpdateNutritionPlanAssignmentCommand, NutritionPlanAssignmentDto?>
{
    public async Task<NutritionPlanAssignmentDto?> Handle(UpdateNutritionPlanAssignmentCommand request, CancellationToken ct)
    {
        var assignment = await repository.GetPlanAssignmentByIdAsync(request.Id, ct);
        if (assignment is null) return null;

        var r = request.Request;
        if (r.StartDate.Date != assignment.StartDate.Date
            || r.EndDate.HasValue && r.EndDate.Value.Date != assignment.EndDate?.Date
            || r.Status == AssignmentStatus.Active && assignment.Status != AssignmentStatus.Active)
            throw new CoppAddresd.Domain.Exceptions.UnprocessableEntityException(
                "La fecha y la activación del ciclo solo pueden establecerse mediante una nueva aprobación nutricional.");
        assignment.Status = r.Status;
        assignment.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
        assignment.UpdatedAt = DateTime.UtcNow;

        await repository.UpdatePlanAssignmentAsync(assignment, ct);

        logger.LogInformation("NutritionPlanAssignment actualizado: {Id}", assignment.Id);

        var updated = await repository.GetPlanAssignmentByIdAsync(assignment.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer la asignación actualizada.");

        return NutritionPlanAssignmentDto.FromEntity(updated);
    }
}

// --- Delete Nutrition Plan Assignment ---

public record DeleteNutritionPlanAssignmentCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteNutritionPlanAssignmentCommandHandler(
    IWellnessRepository repository,
    ILogger<DeleteNutritionPlanAssignmentCommandHandler> logger) : IRequestHandler<DeleteNutritionPlanAssignmentCommand, bool>
{
    public async Task<bool> Handle(DeleteNutritionPlanAssignmentCommand request, CancellationToken ct)
    {
        var assignment = await repository.GetPlanAssignmentByIdAsync(request.Id, ct);
        if (assignment is null) return false;

        await repository.DeletePlanAssignmentAsync(assignment, ct);

        logger.LogInformation("NutritionPlanAssignment eliminado: {Id}", request.Id);
        return true;
    }
}
