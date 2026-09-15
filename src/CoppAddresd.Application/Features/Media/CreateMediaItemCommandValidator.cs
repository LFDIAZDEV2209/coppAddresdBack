using CoppAddresd.Domain.Enums;
using FluentValidation;

namespace CoppAddresd.Application.Features.Media;

public sealed class CreateMediaItemCommandValidator : AbstractValidator<CreateMediaItemCommand>
{
    public CreateMediaItemCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("El título es requerido.").MaximumLength(200);

        RuleFor(x => x.Description).MaximumLength(2000);

        RuleFor(x => x.MediaType).IsInEnum().WithMessage("Tipo de medio inválido.");

        RuleFor(x => x.Category).IsInEnum().WithMessage("Categoría inválida.");

        RuleFor(x => x.StorageKey)
            .NotEmpty()
            .WithMessage("La clave de storage (S3) es requerida.")
            .MaximumLength(1024);

        RuleFor(x => x.ContentType).MaximumLength(100);

        RuleFor(x => x.FileSizeBytes).GreaterThanOrEqualTo(0).When(x => x.FileSizeBytes.HasValue);

        RuleFor(x => x.DurationSecs)
            .GreaterThan(0)
            .When(x => x.DurationSecs.HasValue)
            .WithMessage("La duración debe ser mayor a cero.");

        RuleFor(x => x.Status).IsInEnum().WithMessage("Estado inválido.");

        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
