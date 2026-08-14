using FluentValidation;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Validación del payload de creación. Reglas mínimas: identidad requerida y
/// longitudes de los campos libres. El modelo de pacientes aún es joven y sus
/// columnas pueden evolucionar, por eso no se validan catálogos cerrados
/// (género, tipo de documento, etc.) contra listas fijas.
/// </summary>
public sealed class CreatePatientCommandValidator : AbstractValidator<CreatePatientCommand>
{
    public CreatePatientCommandValidator()
    {
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

        RuleFor(x => x.MedicalRecordNumber)
            .MaximumLength(50);

        When(x => x.VitalSigns is not null, () =>
        {
            RuleForEach(x => x.VitalSigns).ChildRules(vitals =>
            {
                vitals.RuleFor(v => v.Systolic).InclusiveBetween(30, 300);
                vitals.RuleFor(v => v.Diastolic).InclusiveBetween(20, 200);
                vitals.RuleFor(v => v.HeartRate).InclusiveBetween(20, 300);
                vitals.RuleFor(v => v.O2Saturation).InclusiveBetween(50, 100);
                vitals.RuleFor(v => v.TemperatureC).InclusiveBetween(30m, 45m);
                vitals.RuleFor(v => v.HeightCm).InclusiveBetween(30m, 250m);
                vitals.RuleFor(v => v.WeightKg).InclusiveBetween(1m, 500m);
            });
        });
    }
}