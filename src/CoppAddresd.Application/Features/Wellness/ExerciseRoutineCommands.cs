using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Wellness;

// --- List Exercise Routines ---

public record ListExerciseRoutinesQuery(
    string? Search = null, string? Status = null, string? Category = null,
    int Page = 1, int PageSize = 20)
    : IRequest<PaginatedExerciseRoutineResult>;

public sealed class ListExerciseRoutinesQueryHandler(
    IWellnessRepository repository) : IRequestHandler<ListExerciseRoutinesQuery, PaginatedExerciseRoutineResult>
{
    public async Task<PaginatedExerciseRoutineResult> Handle(ListExerciseRoutinesQuery request, CancellationToken ct)
    {
        var (items, total) = await repository.ListRoutinesAsync(
            request.Search, request.Status, request.Category,
            Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 100), ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)Math.Clamp(request.PageSize, 1, 100)));

        return new PaginatedExerciseRoutineResult(
            items.Select(ExerciseRoutineListItemDto.FromEntity).ToList(),
            total, Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 100), totalPages);
    }
}

// --- Get Exercise Routine ---

public record GetExerciseRoutineQuery(Guid Id) : IRequest<ExerciseRoutineDto?>;

public sealed class GetExerciseRoutineQueryHandler(
    IWellnessRepository repository) : IRequestHandler<GetExerciseRoutineQuery, ExerciseRoutineDto?>
{
    public async Task<ExerciseRoutineDto?> Handle(GetExerciseRoutineQuery request, CancellationToken ct)
    {
        var routine = await repository.GetRoutineByIdAsync(request.Id, ct);
        return routine is null ? null : ExerciseRoutineDto.FromEntity(routine);
    }
}

// --- Create Exercise Routine ---

public record CreateExerciseRoutineCommand(CreateExerciseRoutineRequest Request, Guid? CreatedBy = null)
    : IRequest<ExerciseRoutineDto>;

public sealed class CreateExerciseRoutineCommandHandler(
    IWellnessRepository repository,
    ILogger<CreateExerciseRoutineCommandHandler> logger) : IRequestHandler<CreateExerciseRoutineCommand, ExerciseRoutineDto>
{
    public async Task<ExerciseRoutineDto> Handle(CreateExerciseRoutineCommand request, CancellationToken ct)
    {
        var r = request.Request;

        var routine = new ExerciseRoutine
        {
            Id = Guid.NewGuid(),
            Name = r.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim(),
            Difficulty = r.Difficulty,
            EstimatedMinutes = r.EstimatedMinutes,
            Category = r.Category,
            Status = r.Status,
            MediaId = r.MediaId,
            CreatedBy = request.CreatedBy,
            CreatedAt = DateTime.UtcNow,
        };

        if (r.Exercises is { Count: > 0 })
        {
            routine.Exercises = r.Exercises.Select(e => new RoutineExercise
            {
                Id = Guid.NewGuid(),
                Name = e.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(e.Description) ? null : e.Description.Trim(),
                Sets = e.Sets,
                Repetitions = e.Repetitions,
                RestSeconds = e.RestSeconds,
                DurationSecs = e.DurationSecs,
                WeightKg = e.WeightKg,
                MediaId = e.MediaId,
                SortOrder = e.SortOrder,
                CreatedAt = DateTime.UtcNow,
            }).ToList();
        }

        await repository.AddRoutineAsync(routine, ct);

        logger.LogInformation("ExerciseRoutine creado: {Id} ({Name})", routine.Id, routine.Name);

        var created = await repository.GetRoutineByIdAsync(routine.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer la rutina creada.");

        return ExerciseRoutineDto.FromEntity(created);
    }
}

// --- Update Exercise Routine ---

public record UpdateExerciseRoutineCommand(Guid Id, UpdateExerciseRoutineRequest Request)
    : IRequest<ExerciseRoutineDto?>;

public sealed class UpdateExerciseRoutineCommandHandler(
    IWellnessRepository repository,
    ILogger<UpdateExerciseRoutineCommandHandler> logger) : IRequestHandler<UpdateExerciseRoutineCommand, ExerciseRoutineDto?>
{
    public async Task<ExerciseRoutineDto?> Handle(UpdateExerciseRoutineCommand request, CancellationToken ct)
    {
        var routine = await repository.GetRoutineByIdAsync(request.Id, ct);
        if (routine is null) return null;

        var r = request.Request;
        routine.Name = r.Name.Trim();
        routine.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        routine.Difficulty = r.Difficulty;
        routine.EstimatedMinutes = r.EstimatedMinutes;
        routine.Category = r.Category;
        routine.Status = r.Status;
        routine.MediaId = r.MediaId;
        routine.UpdatedAt = DateTime.UtcNow;

        // Reemplazar ejercicios si se proporcionan
        if (r.Exercises is not null)
        {
            await repository.DeleteRoutineExercisesByRoutineIdAsync(routine.Id, ct);

            routine.Exercises = r.Exercises.Select(e => new RoutineExercise
            {
                Id = Guid.NewGuid(),
                RoutineId = routine.Id,
                Name = e.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(e.Description) ? null : e.Description.Trim(),
                Sets = e.Sets,
                Repetitions = e.Repetitions,
                RestSeconds = e.RestSeconds,
                DurationSecs = e.DurationSecs,
                WeightKg = e.WeightKg,
                MediaId = e.MediaId,
                SortOrder = e.SortOrder,
                CreatedAt = DateTime.UtcNow,
            }).ToList();

            await repository.AddRoutineExercisesRangeAsync(routine.Exercises, ct);
        }

        await repository.UpdateRoutineAsync(routine, ct);

        logger.LogInformation("ExerciseRoutine actualizado: {Id}", routine.Id);

        var updated = await repository.GetRoutineByIdAsync(routine.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer la rutina actualizada.");

        return ExerciseRoutineDto.FromEntity(updated);
    }
}

// --- Delete Exercise Routine ---

public record DeleteExerciseRoutineCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteExerciseRoutineCommandHandler(
    IWellnessRepository repository,
    ILogger<DeleteExerciseRoutineCommandHandler> logger) : IRequestHandler<DeleteExerciseRoutineCommand, bool>
{
    public async Task<bool> Handle(DeleteExerciseRoutineCommand request, CancellationToken ct)
    {
        var routine = await repository.GetRoutineByIdAsync(request.Id, ct);
        if (routine is null) return false;

        await repository.DeleteRoutineAsync(routine, ct);

        logger.LogInformation("ExerciseRoutine eliminado: {Id}", request.Id);
        return true;
    }
}
