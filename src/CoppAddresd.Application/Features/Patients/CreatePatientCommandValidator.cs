using FluentValidation;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Validación del payload de creación: identidad requerida, longitudes de los
/// campos libres y whitelist de los vocabularios cerrados (estilo de vida,
/// estado civil y estado del paciente). Las referencias a catálogos (FKs) se
/// validan contra la BD en el handler vía <see cref="CatalogGuard"/>.
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

        RuleFor(x => x.MedicalRecordNumber)
            .MaximumLength(50);

        AddClosedVocabularyRules(
            this,
            x => x.MaritalStatus,
            x => x.SmokingStatus,
            x => x.AlcoholStatus,
            x => x.ExerciseLevel,
            x => x.Disability,
            x => x.HospitalizationHistory,
            x => x.SurgeryHistory,
            x => x.Status);

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

    /// <summary>
    /// Reglas de whitelist compartidas por creación y actualización. Cada
    /// <paramref name="selector"/> apunta a la propiedad del vocabulario en el
    /// tipo del comando (create/update tienen los mismos campos).
    /// </summary>
    internal static void AddClosedVocabularyRules<T>(
        AbstractValidator<T> validator,
        Func<T, string?> maritalStatus,
        Func<T, string?> smokingStatus,
        Func<T, string?> alcoholStatus,
        Func<T, string?> exerciseLevel,
        Func<T, string?> disability,
        Func<T, string?> hospitalizationHistory,
        Func<T, string?> surgeryHistory,
        Func<T, string?> status)
        where T : class
    {
        validator.RuleFor(x => maritalStatus(x))
            .Must(v => PatientOptions.IsAllowed(PatientOptions.MaritalStatuses, v))
            .WithMessage("El estado civil no es un valor válido.");

        validator.RuleFor(x => smokingStatus(x))
            .Must(v => PatientOptions.IsAllowed(PatientOptions.SmokingStatuses, v))
            .WithMessage("El estado de tabaquismo no es un valor válido.");

        validator.RuleFor(x => alcoholStatus(x))
            .Must(v => PatientOptions.IsAllowed(PatientOptions.AlcoholStatuses, v))
            .WithMessage("El consumo de alcohol no es un valor válido.");

        validator.RuleFor(x => exerciseLevel(x))
            .Must(v => PatientOptions.IsAllowed(PatientOptions.ExerciseLevels, v))
            .WithMessage("El nivel de ejercicio no es un valor válido.");

        validator.RuleFor(x => disability(x))
            .Must(v => PatientOptions.IsAllowed(PatientOptions.Disabilities, v))
            .WithMessage("La discapacidad no es un valor válido.");

        validator.RuleFor(x => hospitalizationHistory(x))
            .Must(v => PatientOptions.IsAllowed(PatientOptions.HospitalizationHistories, v))
            .WithMessage("El historial de hospitalizaciones no es un valor válido.");

        validator.RuleFor(x => surgeryHistory(x))
            .Must(v => PatientOptions.IsAllowed(PatientOptions.SurgeryHistories, v))
            .WithMessage("El historial de cirugías no es un valor válido.");

        validator.RuleFor(x => status(x))
            .Must(v => PatientOptions.IsAllowed(PatientOptions.Statuses, v))
            .WithMessage("El estado del paciente no es un valor válido.");
    }
}