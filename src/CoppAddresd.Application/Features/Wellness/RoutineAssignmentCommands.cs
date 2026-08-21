using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Wellness;

// --- List Routine Assignments ---

public record ListRoutineAssignmentsQuery(
    Guid? PatientId = null, Guid? RoutineId = null, string? Status = null,
    int Page = 1, int PageSize = 20)
    : IRequest<PaginatedRoutineAssignmentResult>;

public sealed class ListRoutineAssignmentsQueryHandler(
    IWellnessRepository repository) : IRequestHandler<ListRoutineAssignmentsQuery, PaginatedRoutineAssignmentResult>
{
    public async Task<PaginatedRoutineAssignmentResult> Handle(ListRoutineAssignmentsQuery request, CancellationToken ct)
    {
        var (items, total) = await repository.ListAssignmentsAsync(
            request.PatientId, request.RoutineId, request.Status,
            Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 100), ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)Math.Clamp(request.PageSize, 1, 100)));

        return new PaginatedRoutineAssignmentResult(
            items.Select(RoutineAssignmentDto.FromEntity).ToList(),
            total, Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 100), totalPages);
    }
}

// --- Get Routine Assignment ---

public record GetRoutineAssignmentQuery(Guid Id) : IRequest<RoutineAssignmentDto?>;

public sealed class GetRoutineAssignmentQueryHandler(
    IWellnessRepository repository) : IRequestHandler<GetRoutineAssignmentQuery, RoutineAssignmentDto?>
{
    public async Task<RoutineAssignmentDto?> Handle(GetRoutineAssignmentQuery request, CancellationToken ct)
    {
        var assignment = await repository.GetAssignmentByIdAsync(request.Id, ct);
        return assignment is null ? null : RoutineAssignmentDto.FromEntity(assignment);
    }
}

// --- List Assignments by Patient ---

public record ListAssignmentsByPatientQuery(Guid PatientId) : IRequest<IReadOnlyList<RoutineAssignmentDto>>;

public sealed class ListAssignmentsByPatientQueryHandler(
    IWellnessRepository repository) : IRequestHandler<ListAssignmentsByPatientQuery, IReadOnlyList<RoutineAssignmentDto>>
{
    public async Task<IReadOnlyList<RoutineAssignmentDto>> Handle(ListAssignmentsByPatientQuery request, CancellationToken ct)
    {
        var items = await repository.ListAssignmentsByPatientAsync(request.PatientId, ct);
        return items.Select(RoutineAssignmentDto.FromEntity).ToList();
    }
}

// --- Create Routine Assignment ---

public record CreateRoutineAssignmentCommand(CreateRoutineAssignmentRequest Request, Guid? CreatedBy = null)
    : IRequest<RoutineAssignmentDto>;

public sealed class CreateRoutineAssignmentCommandHandler(
    IWellnessRepository repository,
    ILogger<CreateRoutineAssignmentCommandHandler> logger) : IRequestHandler<CreateRoutineAssignmentCommand, RoutineAssignmentDto>
{
    public async Task<RoutineAssignmentDto> Handle(CreateRoutineAssignmentCommand request, CancellationToken ct)
    {
        var r = request.Request;

        var assignment = new RoutineAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = r.PatientId,
            RoutineId = r.RoutineId,
            StartDate = DateTime.SpecifyKind(r.StartDate, DateTimeKind.Utc),
            EndDate = r.EndDate.HasValue
                ? DateTime.SpecifyKind(r.EndDate.Value, DateTimeKind.Utc)
                : null,
            Frequency = r.Frequency,
            Status = r.Status,
            Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim(),
            CreatedBy = request.CreatedBy,
            CreatedAt = DateTime.UtcNow,
        };

        await repository.AddAssignmentAsync(assignment, ct);

        logger.LogInformation("RoutineAssignment creado: {Id} (Paciente: {PatientId}, Rutina: {RoutineId})",
            assignment.Id, assignment.PatientId, assignment.RoutineId);

        var created = await repository.GetAssignmentByIdAsync(assignment.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer la asignación creada.");

        return RoutineAssignmentDto.FromEntity(created);
    }
}

// --- Update Routine Assignment ---

public record UpdateRoutineAssignmentCommand(Guid Id, UpdateRoutineAssignmentRequest Request)
    : IRequest<RoutineAssignmentDto?>;

public sealed class UpdateRoutineAssignmentCommandHandler(
    IWellnessRepository repository,
    ILogger<UpdateRoutineAssignmentCommandHandler> logger) : IRequestHandler<UpdateRoutineAssignmentCommand, RoutineAssignmentDto?>
{
    public async Task<RoutineAssignmentDto?> Handle(UpdateRoutineAssignmentCommand request, CancellationToken ct)
    {
        var assignment = await repository.GetAssignmentByIdAsync(request.Id, ct);
        if (assignment is null) return null;

        var r = request.Request;
        assignment.StartDate = DateTime.SpecifyKind(r.StartDate, DateTimeKind.Utc);
        assignment.EndDate = r.EndDate.HasValue
            ? DateTime.SpecifyKind(r.EndDate.Value, DateTimeKind.Utc)
            : null;
        assignment.Frequency = r.Frequency;
        assignment.Status = r.Status;
        assignment.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
        assignment.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAssignmentAsync(assignment, ct);

        logger.LogInformation("RoutineAssignment actualizado: {Id}", assignment.Id);

        var updated = await repository.GetAssignmentByIdAsync(assignment.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer la asignación actualizada.");

        return RoutineAssignmentDto.FromEntity(updated);
    }
}

// --- Delete Routine Assignment ---

public record DeleteRoutineAssignmentCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteRoutineAssignmentCommandHandler(
    IWellnessRepository repository,
    ILogger<DeleteRoutineAssignmentCommandHandler> logger) : IRequestHandler<DeleteRoutineAssignmentCommand, bool>
{
    public async Task<bool> Handle(DeleteRoutineAssignmentCommand request, CancellationToken ct)
    {
        var assignment = await repository.GetAssignmentByIdAsync(request.Id, ct);
        if (assignment is null) return false;

        await repository.DeleteAssignmentAsync(assignment, ct);

        logger.LogInformation("RoutineAssignment eliminado: {Id}", request.Id);
        return true;
    }
}
