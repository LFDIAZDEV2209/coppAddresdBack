using FluentValidation;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Validación del payload de actualización. Comparte las mismas reglas de
/// fondo que la creación (identidad requerida y longitudes).
/// </summary>
public sealed class UpdatePatientCommandValidator : AbstractValidator<UpdatePatientCommand>
{
    public UpdatePatientCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El identificador del paciente es requerido.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es requerido.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Los apellidos son requeridos.")
            .MaximumLength(100);

        RuleFor(x => x.MiddleName).MaximumLength(100);
        RuleFor(x => x.DocumentType).MaximumLength(20);
        RuleFor(x => x.DocumentNumber).MaximumLength(50);
        RuleFor(x => x.Gender).MaximumLength(10);
        RuleFor(x => x.Ethnicity).MaximumLength(60);
        RuleFor(x => x.BloodType).MaximumLength(5);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(320);
        RuleFor(x => x.Address).MaximumLength(200);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.State).MaximumLength(2);
        RuleFor(x => x.PostalCode).MaximumLength(10);
        RuleFor(x => x.Status).MaximumLength(20);

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("El correo electrónico no tiene un formato válido.");
    }
}