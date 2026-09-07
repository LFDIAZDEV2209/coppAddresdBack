using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.SetWeekContent;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.SetWeekContentRange;

/// <summary>
/// Command para asignar el mismo contenido (plan de alimentación y/o rutina
/// de ejercicio) a un rango de semanas de la inscripción (ERP, TASK-13b,
/// UC-C2 "Asignar en bloque"). Reusa <see cref="SetWeekContentCommand"/>
/// semana a semana: la validación de cada semana (rango [1..totalWeeks],
/// existencia de plan/rutina) la hace el handler existente, sin duplicarla.
/// </summary>
public sealed record SetWeekContentRangeCommand(
    Guid EnrollmentId,
    int FromWeek,
    int ToWeek,
    Guid? NutritionPlanId,
    Guid? ExerciseRoutineId,
    Guid? ActorId) : IRequest<IReadOnlyList<ProgramContentWeekDto>>;

/// <summary>
/// Aplica el comando de semana sobre <c>[FromWeek..ToWeek]</c> y devuelve el
/// resultado por semana en orden ascendente. Un rango inválido
/// (<c>fromWeek &lt; 1</c> o <c>toWeek &lt; fromWeek</c>) → 422.
/// </summary>
public sealed class SetWeekContentRangeHandler(
    ISender sender) : IRequestHandler<SetWeekContentRangeCommand, IReadOnlyList<ProgramContentWeekDto>>
{
    public async Task<IReadOnlyList<ProgramContentWeekDto>> Handle(
        SetWeekContentRangeCommand request, CancellationToken ct)
    {
        if (request.FromWeek < 1 || request.ToWeek < request.FromWeek)
        {
            throw new UnprocessableEntityException(
                $"INVALID_WEEK_RANGE: el rango [{request.FromWeek}..{request.ToWeek}] es inválido.");
        }

        var results = new List<ProgramContentWeekDto>(request.ToWeek - request.FromWeek + 1);

        for (var week = request.FromWeek; week <= request.ToWeek; week++)
        {
            var weekResult = await sender.Send(
                new SetWeekContentCommand(
                    request.EnrollmentId,
                    week,
                    request.NutritionPlanId,
                    request.ExerciseRoutineId,
                    request.ActorId),
                ct);
            results.Add(weekResult);
        }

        return results;
    }
}