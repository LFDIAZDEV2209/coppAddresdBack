using FluentValidation;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Validación del payload de actualización: identidad requerida, longitudes
/// y whitelist de vocabularios cerrados (compartidas con la creación).
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
        RuleFor(x => x.DocumentNumber).MaximumLength(50);
        RuleFor(x => x.Gender).MaximumLength(10);
        RuleFor(x => x.PhoneCountryCode).MaximumLength(10);
        RuleFor(x => x.PhoneNumber).MaximumLength(20);
        RuleFor(x => x.Email).MaximumLength(320);
        RuleFor(x => x.Address).MaximumLength(200);
        RuleFor(x => x.PostalCode).MaximumLength(10);
        RuleFor(x => x.Status).MaximumLength(20);

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("El correo electrónico no tiene un formato válido.");

        CreatePatientCommandValidator.AddClosedVocabularyRules(
            this,
            x => x.MaritalStatus,
            x => x.SmokingStatus,
            x => x.AlcoholStatus,
            x => x.ExerciseLevel,
            x => x.Disability,
            x => x.HospitalizationHistory,
            x => x.SurgeryHistory,
            x => x.Status);
    }
}