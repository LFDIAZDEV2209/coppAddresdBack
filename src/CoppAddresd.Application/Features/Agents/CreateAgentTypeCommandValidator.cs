using FluentValidation;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>Validación del payload de creación de tipo de agente.</summary>
public sealed class CreateAgentTypeCommandValidator : AbstractValidator<CreateAgentTypeCommand>
{
    public CreateAgentTypeCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es requerido.")
            .MaximumLength(100);

        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Specialty).MaximumLength(100);
        RuleFor(x => x.IconKey).MaximumLength(50);
        RuleFor(x => x.Slug).MaximumLength(50);
    }
}