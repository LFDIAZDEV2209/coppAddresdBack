using FluentValidation;
using System.Text.Json;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>Validación de creación de versión de agente.</summary>
public sealed class CreateAgentTypeVersionCommandValidator : AbstractValidator<CreateAgentTypeVersionCommand>
{
    public CreateAgentTypeVersionCommandValidator()
    {
        RuleFor(x => x.AgentTypeId).NotEmpty();

        RuleFor(x => x.Config)
            .NotEmpty().WithMessage("La configuración es requerida.")
            .Must(BeValidJson).WithMessage("La configuración debe ser JSON válido.");

        RuleFor(x => x.Notes).MaximumLength(500);
    }

    private static bool BeValidJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        try
        {
            using var doc = JsonDocument.Parse(value);
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}