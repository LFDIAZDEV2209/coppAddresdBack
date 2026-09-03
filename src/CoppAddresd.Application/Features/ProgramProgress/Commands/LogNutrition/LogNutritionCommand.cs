using CoppAddresd.Application.Features.ProgramProgress.DTOs.Nutrition;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.LogNutrition;

/// <summary>
/// Registro de una comida o hidratación del paciente (SPEC §18, B): cierra el
/// bucle de la NutritionPage del móvil (4 comidas <c>des</c>/<c>alm</c>/
/// <c>mer</c>/<c>cen</c> + hidratación <c>agua</c>). El <c>patientId</c>
/// SIEMPRE llega resuelto de la identidad del JWT por la capa API (nunca del
/// body): es la base del anti-IDOR (AC-11) — un cruce entre pacientes devuelve
/// 404, nunca 403. El <c>localDate</c> opcional se resuelve contra el hoy local
/// del paciente (una fecha futura → 422 <c>INVALID_DATE</c>, autoridad del
/// repositorio).
///
/// Otorga la XP granular por el camino del catálogo de reglas
/// (<c>NUTRITION_MEAL_COMPLETE</c> / <c>NUTRITION_HYDRATION</c>, tope 4/día y
/// 1/día respectivamente) de forma ADITIVA a la tarea <c>nut</c> existente (la
/// tarea del programa sigue otorgando sus puntos de plantilla al completarse;
/// el log granular es XP adicional por registro, SPEC §18, decisión 24). El log
/// NO auto-completa la tarea <c>nut</c>: eso sigue viviendo en el flujo de
/// <c>POST /tasks/complete</c> sin cambios.
/// </summary>
public sealed record LogNutritionCommand(
    Guid PatientId,
    MealCode MealCode,
    DateOnly? LocalDate = null,
    Guid? ActorId = null,
    NutritionIntakePayload? Intake = null) : IRequest<NutritionLogResultDto>;

/// <summary>
/// Validación de forma del payload (SPEC §18, B): el <c>mealCode</c> es un
/// código conocido de comida/hidratación (des/alm/mer/cen/agua) y la fecha, si
/// viene, es una fecha válida. Las reglas de negocio (hoy local, duplicado,
/// plantilla sembrada, tope diario/semanal) viven en el repositorio — misma
/// división que CompleteTask (forma aquí, negocio en el repo).
/// </summary>
public sealed class LogNutritionCommandValidator : AbstractValidator<LogNutritionCommand>
{
    public LogNutritionCommandValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty()
            .WithMessage("El patientId es requerido.");

        RuleFor(x => x.MealCode)
            .IsInEnum()
            .WithMessage("Código de comida/hidratación inválido (des/alm/mer/cen/agua).");

        // Intake enriquecido opcional (SPEC nutrition-intake-adherence): los
        // rangos 0–5000 los valida NutritionIntakePayloadValidator; un intake
        // null (shape anterior) sigue siendo válido.
        RuleFor(x => x.Intake)
            .SetValidator(new NutritionIntakePayloadValidator())
            .When(x => x.Intake is not null);
    }
}

/// <summary>
/// Orquesta el log nutricional vía el repositorio (una transacción con la
/// inscripción bloqueada FOR UPDATE: <c>habit_check</c> + XP granular
/// idempotente). El log estructurado no transporta PHI (solo ids, código de
/// comida, fecha y XP otorgada).
/// </summary>
public sealed class LogNutritionCommandHandler(
    IProgramRepository repository,
    ILogger<LogNutritionCommandHandler> logger) : IRequestHandler<LogNutritionCommand, NutritionLogResultDto>
{
    public async Task<NutritionLogResultDto> Handle(LogNutritionCommand request, CancellationToken ct)
    {
        var result = await repository.LogNutritionAsync(
            request.PatientId,
            request.MealCode,
            request.LocalDate,
            request.Intake,
            request.ActorId,
            ct);

        logger.LogInformation(
            "Program.NutritionLogged: patient={PatientId} meal={MealCode} fecha={LocalDate} " +
            "xpAwarded={XpAwarded} balance={XpBalance} actor={ActorId}",
            request.PatientId, result.MealCode, result.LocalDate,
            result.XpAwarded, result.XpBalanceAfter, request.ActorId);

        return result;
    }
}