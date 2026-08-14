using FluentValidation;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>Validación de creación de knowledge base.</summary>
public sealed class CreateKnowledgeBaseCommandValidator : AbstractValidator<CreateKnowledgeBaseCommand>
{
    public CreateKnowledgeBaseCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es requerido.")
            .MaximumLength(100);

        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Scope)
            .NotEmpty().WithMessage("El alcance es requerido.")
            .Must(BeValidScope).WithMessage("El alcance debe ser 'Global' o 'Agent'.");
    }

    private static bool BeValidScope(string? scope)
        => scope is not null &&
           (scope.Equals("Global", StringComparison.OrdinalIgnoreCase) ||
            scope.Equals("Agent", StringComparison.OrdinalIgnoreCase));
}