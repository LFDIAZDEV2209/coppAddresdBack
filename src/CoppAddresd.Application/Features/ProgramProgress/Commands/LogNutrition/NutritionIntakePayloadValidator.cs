using CoppAddresd.Application.Features.ProgramProgress.DTOs.Nutrition;
using FluentValidation;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.LogNutrition;

/// <summary>
/// Validación de forma del intake opcional de <c>POST /api/v1/program/nutrition/log</c>
/// (SPEC nutrition-intake-adherence): los 8 campos son opcionales y sus rangos
/// replican el precedente de signos vitales (kcal/macros/agua 0–5000;
/// negativos → error de validación). Solo valida forma: las reglas de negocio
/// (hoy local, duplicado 409, ownership D6 del <c>foodAnalysisId</c>,
/// resolución del plan-day) viven en el repositorio — misma división que
/// <see cref="LogNutritionCommandValidator"/>.
/// </summary>
public sealed class NutritionIntakePayloadValidator : AbstractValidator<NutritionIntakePayload>
{
    private const int MaxIntakeValue = 5000;

    public NutritionIntakePayloadValidator()
    {
        RuleFor(x => x.Calories)
            .InclusiveBetween(0, MaxIntakeValue)
            .WithMessage("Las calorías deben estar entre 0 y 5000 kcal.");

        RuleFor(x => x.ProteinG)
            .InclusiveBetween(0, MaxIntakeValue)
            .WithMessage("La proteína debe estar entre 0 y 5000 g.");

        RuleFor(x => x.CarbsG)
            .InclusiveBetween(0, MaxIntakeValue)
            .WithMessage("Los carbohidratos deben estar entre 0 y 5000 g.");

        RuleFor(x => x.FatG)
            .InclusiveBetween(0, MaxIntakeValue)
            .WithMessage("La grasa debe estar entre 0 y 5000 g.");

        RuleFor(x => x.FiberG)
            .InclusiveBetween(0, MaxIntakeValue)
            .WithMessage("La fibra debe estar entre 0 y 5000 g.");

        RuleFor(x => x.WaterMl)
            .InclusiveBetween(0, MaxIntakeValue)
            .WithMessage("El agua debe estar entre 0 y 5000 ml.");
    }
}