using CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.CalculateScores;

/// <summary>
/// Recálculo forzado de puntajes (SPEC §13.7.2): el clínico pide un
/// recompute explícito con <c>{ patientId, periodEndLocalDate? }</c>. Es el
/// único disparador fuera de banda del motor (no hay cron en MVP, SPEC §13.3).
/// La capa API lo protege con <c>Program.Edit</c> (SPEC §13.6: sin código de
/// permiso nuevo). El <c>periodEndLocalDate</c> opcional fija el fin del
/// período del Índice de Salud en fecha local del paciente (una fecha futura
/// se rechaza con 422 <c>INVALID_PERIOD</c> por el repositorio, autoridad del
/// tiempo local). El Índice de Transformación sigue anclado a la semana del
/// programa (SPEC §13.2) y se recalcula igualmente.
/// </summary>
public sealed record CalculateScoresCommand(
    Guid PatientId,
    DateOnly? PeriodEndLocalDate = null) : IRequest<ScoresResponseDto>;

/// <summary>
/// Validación de forma del payload (T-38): el <c>patientId</c> es requerido.
/// El formato de <c>periodEndLocalDate</c> lo garantiza el binding JSON
/// (<c>DateOnly</c> ISO) y la regla de negocio "no futura" la resuelve el
/// repositorio contra el hoy local del paciente (SPEC §13.7.2, 422
/// INVALID_PERIOD) — misma división que CompleteTask (forma aquí, negocio en
/// el repo).
/// </summary>
public sealed class CalculateScoresCommandValidator : AbstractValidator<CalculateScoresCommand>
{
    public CalculateScoresCommandValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty()
            .WithMessage("El patientId es requerido.");
    }
}

/// <summary>
/// Orquesta el recálculo manual clínico: fuerza la recomputación de ambos
/// puntajes (health con el fin de período opcional) y devuelve el shape
/// SPEC §13.7.1. Tras persistir la fila nueva de <c>health_scores</c>, evalúa
/// la XP clínica del período (SPEC §15, C): este es el ÚNICO disparador del
/// otorgamiento clínico — <c>GET /scores</c> nunca otorga XP (solo computa y
/// devuelve la cola de pendientes). El header <c>X-Score-Recalculated: true</c>
/// lo emite la capa API (el handler no conoce HTTP). Paciente sin inscripción
/// activa → 404 <c>NOT_FOUND</c> (anti-IDOR: el clínico no distingue si el
/// paciente existe).
/// </summary>
public sealed class CalculateScoresCommandHandler(
    IProgramRepository repository,
    IWeaknessDetectionService weaknessDetection,
    ILogger<CalculateScoresCommandHandler> logger) : IRequestHandler<CalculateScoresCommand, ScoresResponseDto>
{
    public async Task<ScoresResponseDto> Handle(CalculateScoresCommand request, CancellationToken ct)
    {
        var health = await repository.GetOrComputeHealthScoreAsync(
            request.PatientId, ScoreTrigger.ManualRecompute,
            force: true, periodEndLocalDate: request.PeriodEndLocalDate, ct: ct)
            ?? throw new NotFoundException(
                $"NOT_FOUND: no existe una inscripción activa para el paciente {request.PatientId}.");

        var transformation = await repository.GetOrComputeTransformationScoreAsync(
            request.PatientId, ScoreTrigger.ManualRecompute, force: true, ct: ct)
            ?? throw new NotFoundException(
                $"NOT_FOUND: no existe una inscripción activa para el paciente {request.PatientId}.");

        // SPEC §15, C: la XP clínica se evalúa SOLO en este disparador (después
        // de persistir la fila de health_scores, cuyo id es el source_ref_id
        // del dedupe clinical_period). El GET /scores NO otorga XP clínica.
        var clinicalXp = await repository.EvaluateClinicalXpAwardsAsync(
            request.PatientId, request.PeriodEndLocalDate, ct);

        // SPEC §18, C: los otorgamientos semanales de nutrición (adherencia
        // ≥ 85% y recuperación +20pp vs el período anterior) también se
        // evalúan SOLO aquí, después de persistir la fila de health_scores
        // (source_ref_id del dedupe nutrition_period). El GET /scores NO otorga
        // XP de nutrición. Aditivo a la tarea nut existente (SPEC §18, decisión 24).
        var nutritionAwards = await repository.EvaluateNutritionAwardsAsync(
            request.PatientId, request.PeriodEndLocalDate, ct);

        // SPEC §21, C ("Paso 7c"): la detección de debilidades corre SOLO en
        // este disparador, una vez por recálculo, después de puntajes + XP
        // clínica + premios semanales (el paquete semanal lee la fila fresca de
        // health_scores). Idempotente por el dedupe de estado abierto (AC-43):
        // re-correr /calculate no duplica debilidades abiertas (AC-45).
        var weaknesses = await weaknessDetection.DetectAndPersistAsync(
            request.PatientId, ct);

        logger.LogInformation(
            "Program.ScoresRecalculated: patient={PatientId} health={HealthScore} " +
            "transformation={TransformationScore} clinicalReviews={ReviewsCreated} " +
            "clinicalXp={ClinicalXp} clinicalRules={ClinicalRules} " +
            "nutritionXp={NutritionXp} nutritionRules={NutritionRules} " +
            "weaknessRules={WeaknessRules} weaknessesPersisted={WeaknessesPersisted} actor={ActorId}",
            request.PatientId, health.Current, transformation.Current,
            clinicalXp.ReviewsCreated, clinicalXp.TotalXpAwarded,
            string.Join(",", clinicalXp.AwardedRules),
            nutritionAwards.TotalXpAwarded, string.Join(",", nutritionAwards.AwardedRules),
            weaknesses.RulesFired, weaknesses.NewWeaknessesPersisted,
            request.PatientId);

        return new ScoresResponseDto(health, transformation);
    }
}