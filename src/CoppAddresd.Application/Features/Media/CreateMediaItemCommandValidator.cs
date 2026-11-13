using CoppAddresd.Domain.Enums;
using FluentValidation;

namespace CoppAddresd.Application.Features.Media;

public sealed class CreateMediaItemCommandValidator : AbstractValidator<CreateMediaItemCommand>
{
    public CreateMediaItemCommandValidator()
    {
        RuleFor(x => x).Must(x => ExternalMediaRules.IsValid(x.SourceKind, x.Provider, x.ExternalUrl, x.MediaType))
            .WithMessage("Origen, proveedor o URL de video inválidos; se admiten enlaces HTTPS de YouTube y Vimeo.");
        RuleFor(x => x.Tags).Must(ExternalMediaRules.ValidTags).WithMessage("Máximo 32 etiquetas, de 1 a 64 caracteres.");

        RuleFor(x => x.Title).NotEmpty().WithMessage("El título es requerido.").MaximumLength(200);

        RuleFor(x => x.Description).MaximumLength(2000);

        RuleFor(x => x.MediaType).IsInEnum().WithMessage("Tipo de medio inválido.");

        RuleFor(x => x.Category).IsInEnum().WithMessage("Categoría inválida.");

        RuleFor(x => x.StorageKey)
            .NotEmpty()
            .WithMessage("La clave de storage (S3) es requerida.")
            .MaximumLength(1024)
            .When(x => ExternalMediaRules.NormalizeSource(x.SourceKind) == "file");

        RuleFor(x => x.ContentType).MaximumLength(100);
        RuleFor(x => x.ContentType)
            .Must(value => value is not null && MediaPublishRules.IsAllowedContentType(MediaType.Image, value))
            .When(x => x.MediaType == MediaType.Image)
            .WithMessage("La imagen debe ser JPEG, PNG, WebP o GIF.");

        RuleFor(x => x.FileSizeBytes).GreaterThanOrEqualTo(0).When(x => x.FileSizeBytes.HasValue);

        RuleFor(x => x.DurationSecs)
            .GreaterThan(0)
            .When(x => x.DurationSecs.HasValue)
            .WithMessage("La duración debe ser mayor a cero.");

        RuleFor(x => x.Status).IsInEnum().WithMessage("Estado inválido.");

        RuleFor(x => x.StorageKey)
            .Must(key => !MediaPublishRules.IsPlaceholderStorageKey(key))
            .When(x => x.Status == MediaStatus.Published)
            .WithMessage("El audio de demostración no se puede publicar. Carga la grabación real.");

        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
