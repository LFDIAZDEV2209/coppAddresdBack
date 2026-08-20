using FluentValidation;

namespace CoppAddresd.Application.Features.Documents;

/// <summary>Validación de creación de documentos (metadata).</summary>
public sealed class CreateDocumentCommandValidator : AbstractValidator<CreateDocumentCommand>
{
    public CreateDocumentCommandValidator()
    {
        // El paciente es obligatorio salvo al crear una versión (parentDocumentId):
        // en ese caso paciente y clínica se heredan del documento raíz.
        RuleFor(x => x.PatientId).NotEmpty()
            .WithMessage("El documento requiere un paciente.")
            .When(x => x.ParentDocumentId is null);
        RuleFor(x => x.DocumentTypeId).NotEmpty().WithMessage("El tipo de documento es requerido.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.StorageKey).NotEmpty().MaximumLength(500);
        RuleFor(x => x.FileSizeBytes).GreaterThanOrEqualTo(0).When(x => x.FileSizeBytes.HasValue);
    }
}

/// <summary>Validación de actualización de documentos.</summary>
public sealed class UpdateDocumentCommandValidator : AbstractValidator<UpdateDocumentCommand>
{
    public UpdateDocumentCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).MaximumLength(200).When(x => x.Title is not null);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
    }
}