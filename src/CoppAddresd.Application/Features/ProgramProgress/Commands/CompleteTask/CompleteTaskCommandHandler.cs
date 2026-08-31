using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.CompleteTask;

/// <summary>
/// Orquesta la completación de una tarea (SPEC §6.2):
/// 1. Rechaza <c>emocional</c> sin <c>moodScore</c> (nunca fabrica datos
///    clínicos; el repositorio también lo exige, defensa en profundidad).
/// 2. Chequea "due today": la fecha no puede ser futura en la zona del
///    paciente (SPEC §6.1); completaciones pasadas (offline) se permiten.
/// 3. Delega la persistencia idempotente a <c>IProgramRepository</c>.
/// 4. Mapea el resultado tipado (primera escritura vs replay) al shape §7.2;
///    una clave de idempotencia reutilizada con otra tarea/fecha es 409 (AC-04).
/// El actor (para auditoría) llega en <paramref name="CompleteTaskCommand.ActorId"/>;
/// la capa API lo resuelve desde <c>ICurrentContext</c> (precedente del repo).
/// </summary>
public sealed class CompleteTaskCommandHandler(
    IProgramRepository repository,
    ILogger<CompleteTaskCommandHandler> logger) : IRequestHandler<CompleteTaskCommand, CompleteTaskResponseDto>
{
    public async Task<CompleteTaskResponseDto> Handle(CompleteTaskCommand request, CancellationToken ct)
    {
        if (request.TaskCode == TaskCode.emocional && !request.MoodScore.HasValue)
        {
            throw new UnprocessableEntityException(
                "MOOD_SCORE_REQUIRED: la tarea emocional requiere moodScore (1..5).");
        }

        var todayLocal = await repository.GetPatientLocalTodayAsync(request.EnrollmentId, ct)
            ?? throw new NotFoundException(
                $"NO_ACTIVE_ENROLLMENT: no existe la inscripción {request.EnrollmentId}.");

        if (request.LocalDate > todayLocal)
        {
            throw new UnprocessableEntityException(
                $"DATE_OUTSIDE_ACTIVE_WEEK: la fecha {request.LocalDate:yyyy-MM-dd} es futura " +
                $"(hoy local del paciente: {todayLocal:yyyy-MM-dd}).");
        }

        var input = new CompleteTaskInput(
            request.EnrollmentId,
            request.LocalDate,
            request.TaskCode,
            request.ClientRequestId,
            request.ClientCompletedAt,
            request.MoodScore,
            request.Barriers,
            request.ContentFingerprint);

        var result = await repository.CompleteTaskAsync(input, ct);

        if (result.Outcome == CompleteTaskOutcome.IdempotencyKeyReused)
        {
            throw new BusinessRuleViolationException(
                "IDEMPOTENCY_KEY_REUSED: la clave clientRequestId ya fue usada para otra tarea o fecha.");
        }

        logger.LogInformation(
            "Program.CompleteTask: enrollment={EnrollmentId} fecha={LocalDate} tarea={TaskCode} " +
            "outcome={Outcome} puntos={Points} balance={Balance}",
            request.EnrollmentId, request.LocalDate, request.TaskCode,
            result.Outcome, result.PointsAwarded, result.XpBalanceAfter);

        return CompleteTaskResponseDto.FromResult(result);
    }
}