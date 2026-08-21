using FluentValidation;

namespace CoppAddresd.Application.Features.LegalDocuments;

public sealed class SaveDraftCommandValidator : AbstractValidator<SaveDraftCommand>
{
    public SaveDraftCommandValidator()
    {
        RuleFor(command => command.Code).NotEmpty().MaximumLength(60)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("El código debe usar minúsculas, números y guiones.");
        RuleFor(command => command.Request.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Request.Content).NotEmpty()
            .WithMessage("El contenido del documento es requerido.");
        RuleFor(command => command.Request.CreatedBy).MaximumLength(150)
            .When(command => command.Request.CreatedBy is not null);
    }
}
