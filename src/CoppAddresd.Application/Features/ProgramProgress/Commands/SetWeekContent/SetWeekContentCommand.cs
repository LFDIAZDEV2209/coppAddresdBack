using CoppAddresd.Application.DTOs.ProgramProgress;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.SetWeekContent;

/// <summary>
/// Command para configurar el contenido de nutrición y ejercicio de una semana
/// específica de la inscripción (T-77). Consumido por el ERP para asignar/
/// desasignar planes/rutinas por semana.
/// </summary>
public sealed record SetWeekContentCommand(
    Guid EnrollmentId,
    int WeekNumber,
    Guid? NutritionPlanId,
    Guid? ExerciseRoutineId,
    Guid? ActorId) : IRequest<ProgramContentWeekDto>;
