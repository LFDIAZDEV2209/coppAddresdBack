using CoppAddresd.Application.Features.ProgramProgress.DTOs.Nutrition;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.LogNutrition;

/// <summary>
/// Edición del intake de una comida/hidratación ya registrada (móvil):
/// actualiza la fila de <c>app.nutrition_intake_logs</c> anclada al MISMO
/// <c>habit_check</c> (sin duplicados, sin XP adicional). Sin log para esa
/// comida/fecha → 404 <c>NUTRITION_LOG_NOT_FOUND</c>. Misma resolución de
/// identidad (patientId del JWT), fecha local y ownership de
/// <c>foodAnalysisId</c> que el POST.
/// </summary>
public sealed record UpdateNutritionIntakeCommand(
    Guid PatientId,
    MealCode MealCode,
    DateOnly? LocalDate = null,
    Guid? ActorId = null,
    NutritionIntakePayload? Intake = null
) : IRequest<NutritionLogResultDto>;

/// <summary>
/// Validación de forma (misma división que el POST: forma aquí, negocio en
/// el repositorio).
/// </summary>
public sealed class UpdateNutritionIntakeCommandValidator
    : AbstractValidator<UpdateNutritionIntakeCommand>
{
    public UpdateNutritionIntakeCommandValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("El patientId es requerido.");

        RuleFor(x => x.MealCode)
            .IsInEnum()
            .WithMessage("Código de comida/hidratación inválido (des/alm/mer/cen/agua).");

        RuleFor(x => x.Intake!)
            .SetValidator(new NutritionIntakePayloadValidator())
            .When(x => x.Intake is not null);
    }
}

/// <summary>
/// Orquesta la edición vía el repositorio (misma transacción con inscripción
/// bloqueada FOR UPDATE que el POST). Sin PHI en logs.
/// </summary>
public sealed class UpdateNutritionIntakeCommandHandler(
    IProgramRepository repository,
    ILogger<UpdateNutritionIntakeCommandHandler> logger
) : IRequestHandler<UpdateNutritionIntakeCommand, NutritionLogResultDto>
{
    public async Task<NutritionLogResultDto> Handle(
        UpdateNutritionIntakeCommand request,
        CancellationToken ct
    )
    {
        var result = await repository.UpdateNutritionIntakeAsync(
            request.PatientId,
            request.MealCode,
            request.LocalDate,
            request.Intake,
            request.ActorId,
            ct
        );

        logger.LogInformation(
            "Program.NutritionIntakeUpdated: meal={MealCode} fecha={LocalDate}",
            result.MealCode,
            result.LocalDate
        );

        return result;
    }
}
