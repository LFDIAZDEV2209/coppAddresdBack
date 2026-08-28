using CoppAddresd.Application.Features.HealthTests.Assignments;
using CoppAddresd.Application.Features.HealthTests.Catalog;
using CoppAddresd.Application.Features.HealthTests.Execution;
using CoppAddresd.Application.Features.HealthTests.Scoring;
using FluentValidation;

namespace CoppAddresd.Application.Features.HealthTests;

// --- Instrumento ---

public sealed class CreateInstrumentCommandValidator : AbstractValidator<CreateInstrumentCommand>
{
    public CreateInstrumentCommandValidator()
    {
        RuleFor(x => x.Request.Code)
            .NotEmpty()
            .WithMessage("El código es requerido.")
            .Matches("^[a-z0-9_\\-]+$")
            .WithMessage("El código solo admite minúsculas, números, guiones y guiones bajos.")
            .MaximumLength(64);
        RuleFor(x => x.Request.Name)
            .NotEmpty()
            .WithMessage("El nombre es requerido.")
            .MaximumLength(200);
        RuleFor(x => x.Request.Description).MaximumLength(1000);
        RuleFor(x => x.Request.Category).MaximumLength(100);
        RuleFor(x => x.Request.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateInstrumentCommandValidator : AbstractValidator<UpdateInstrumentCommand>
{
    public UpdateInstrumentCommandValidator()
    {
        RuleFor(x => x.Request.Name)
            .NotEmpty()
            .WithMessage("El nombre es requerido.")
            .MaximumLength(200);
        RuleFor(x => x.Request.Description).MaximumLength(1000);
        RuleFor(x => x.Request.Category).MaximumLength(100);
        RuleFor(x => x.Request.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateVersionCommandValidator : AbstractValidator<CreateVersionCommand>
{
    public CreateVersionCommandValidator()
    {
        RuleFor(x => x.Request.VersionNumber).GreaterThan(0);
        RuleFor(x => x.Request.Name).MaximumLength(200);
        RuleFor(x => x.Request.ScoringStrategy).IsInEnum();
        RuleFor(x => x.Request.Points).GreaterThan(0).When(x => x.Request.Points.HasValue);
    }
}

// --- Batería ---

public sealed class CreateBatteryCommandValidator : AbstractValidator<CreateBatteryCommand>
{
    public CreateBatteryCommandValidator()
    {
        RuleFor(x => x.Request.Code)
            .NotEmpty()
            .WithMessage("El código es requerido.")
            .MaximumLength(64);
        RuleFor(x => x.Request.Name)
            .NotEmpty()
            .WithMessage("El nombre es requerido.")
            .MaximumLength(200);
        RuleFor(x => x.Request.Description).MaximumLength(1000);
        RuleFor(x => x.Request.Items)
            .NotEmpty()
            .WithMessage("La batería debe contener al menos un test.")
            .Must(items => items.Select(i => i.InstrumentId).Distinct().Count() == items.Count)
            .WithMessage("La batería no puede repetir el mismo instrumento.")
            .When(x => x.Request.Items is { Count: > 0 });
    }
}

// --- Asignación ---

public sealed class AssignBatteryCommandValidator : AbstractValidator<AssignBatteryCommand>
{
    public AssignBatteryCommandValidator()
    {
        RuleFor(x => x.PatientIds).NotEmpty().WithMessage("Debe indicar al menos un paciente.");
        RuleFor(x => x.PatientIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("No se repiten pacientes en la asignación.")
            .When(x => x.PatientIds is { Count: > 0 });
    }
}

public sealed class AssignTestCommandValidator : AbstractValidator<AssignTestCommand>
{
    public AssignTestCommandValidator()
    {
        RuleFor(x => x.Priority).InclusiveBetween(1, 5).When(x => x.Priority.HasValue);
    }
}

// --- Ejecución ---

public sealed class SubmitEvaluationCommandValidator : AbstractValidator<SubmitEvaluationCommand>
{
    public SubmitEvaluationCommandValidator()
    {
        RuleFor(x => x.Request.Answers)
            .NotEmpty()
            .WithMessage("Debe enviar al menos una respuesta.");
        RuleForEach(x => x.Request.Answers)
            .ChildRules(answers =>
            {
                answers
                    .RuleFor(a => a.QuestionId)
                    .NotEmpty()
                    .WithMessage("El id de la pregunta es requerido.");
                answers.RuleFor(a => a.ValueText).MaximumLength(4000);
            });
    }
}

// --- Configuración de indicadores y reglas de alerta (validación de jsonb) ---

/// <summary>
/// Valida el JSON de <c>computation</c> de un indicador contra el contrato del
/// motor (SPEC A11): fórmula conocida, fuentes con código no vacío.
/// </summary>
public sealed class IndicatorComputationValidator : AbstractValidator<string>
{
    public IndicatorComputationValidator()
    {
        RuleFor(x => x)
            .NotEmpty()
            .WithMessage("La configuración del indicador es requerida.")
            .Must(IsValidComputation)
            .WithMessage("Configuración de indicador inválida (formula + sources requeridos).");
    }

    private static bool IsValidComputation(string json)
    {
        var engine = new IndicatorEngine();
        var computation = engine.Parse(json);
        return computation.Sources.Count > 0
            && computation.Sources.All(s => !string.IsNullOrWhiteSpace(s.Code))
            && (computation.Formula is "sum" or "avg" or "weighted");
    }
}

/// <summary>
/// Valida el JSON de <c>condition</c> de una regla de alerta (SPEC A12):
/// resultado con tipo y código.
/// </summary>
public sealed class AlertConditionValidator : AbstractValidator<string>
{
    public AlertConditionValidator()
    {
        RuleFor(x => x)
            .NotEmpty()
            .WithMessage("La condición de la regla es requerida.")
            .Must(IsValidCondition)
            .WithMessage("Condición de alerta inválida (when.resultType + when.code requeridos).");
    }

    private static bool IsValidCondition(string json)
    {
        var engine = new AlertEngine();
        var condition = engine.Parse(json);
        return condition.When is not null
            && !string.IsNullOrWhiteSpace(condition.When.ResultType)
            && !string.IsNullOrWhiteSpace(condition.When.Code);
    }
}
