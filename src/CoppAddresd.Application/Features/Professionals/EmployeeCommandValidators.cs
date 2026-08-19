using System.Linq.Expressions;
using FluentValidation;

namespace CoppAddresd.Application.Features.Professionals;

/// <summary>Validación de comandos del directorio de empleados.</summary>
public sealed class EmployeeCommandValidators
{
    public sealed class CreateEmployeeValidator : AbstractValidator<CreateEmployeeCommand>
    {
        public CreateEmployeeValidator()
        {
            RuleFor(x => x.OrganizationId).NotEmpty();
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("El nombre es requerido.")
                .MaximumLength(100);
            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Los apellidos son requeridos.")
                .MaximumLength(100);
            RuleFor(x => x.MiddleName).MaximumLength(100);
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("El correo es requerido.")
                .MaximumLength(320)
                .EmailAddress().WithMessage("El correo no tiene un formato válido.");
            RuleFor(x => x.PhoneCountryCode).MaximumLength(10);
            RuleFor(x => x.PhoneNumber).MaximumLength(20);
            RuleFor(x => x.JobTitle).MaximumLength(100);
            RuleFor(x => x.Department).MaximumLength(100);
            RuleFor(x => x.Bio).MaximumLength(5000);

            RuleFor(x => x.Status)
                .Must(v => ProfessionalOptions.IsAllowed(ProfessionalOptions.EmployeeStatuses, v))
                .WithMessage("El estado del empleado no es un valor válido.");

            ClinicAssignmentRules(this, x => x.Clinics);
            LicenseRules(this, x => x.Licenses);
        }
    }

    public sealed class UpdateEmployeeValidator : AbstractValidator<UpdateEmployeeCommand>
    {
        public UpdateEmployeeValidator()
        {
            RuleFor(x => x.FirstName).MaximumLength(100);
            RuleFor(x => x.LastName).MaximumLength(100);
            RuleFor(x => x.MiddleName).MaximumLength(100);
            RuleFor(x => x.Email)
                .MaximumLength(320)
                .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
                .WithMessage("El correo no tiene un formato válido.");
            RuleFor(x => x.PhoneCountryCode).MaximumLength(10);
            RuleFor(x => x.PhoneNumber).MaximumLength(20);
            RuleFor(x => x.JobTitle).MaximumLength(100);
            RuleFor(x => x.Department).MaximumLength(100);
            RuleFor(x => x.Bio).MaximumLength(5000);

            RuleFor(x => x.Status)
                .Must(v => ProfessionalOptions.IsAllowed(ProfessionalOptions.EmployeeStatuses, v))
                .WithMessage("El estado del empleado no es un valor válido.");

            ClinicAssignmentRules(this, x => x.Clinics);
            LicenseRules(this, x => x.Licenses);
        }
    }

    /// <summary>Whitelist de los vocabularios cerrados de las asignaciones a clínicas.</summary>
    private static void ClinicAssignmentRules<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, IEnumerable<ClinicAssignmentInput>?>> selector)
        where T : class
    {
        validator.RuleForEach(selector)
            .ChildRules(clinic =>
            {
                clinic.RuleFor(c => c.ClinicId).NotEmpty();
                clinic.RuleFor(c => c.Status)
                    .Must(v => ProfessionalOptions.IsAllowed(ProfessionalOptions.AssignmentStatuses, v))
                    .WithMessage("El estado de la asignación no es un valor válido.");
            });
    }

    /// <summary>Whitelist de los vocabularios cerrados de las licencias.</summary>
    private static void LicenseRules<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, IEnumerable<LicenseInput>?>> selector)
        where T : class
    {
        validator.RuleForEach(selector)
            .ChildRules(license =>
            {
                license.RuleFor(l => l.LicenseType)
                    .NotEmpty().WithMessage("El tipo de credencial es requerido.")
                    .Must(v => ProfessionalOptions.IsAllowed(ProfessionalOptions.LicenseTypes, v))
                    .WithMessage("El tipo de credencial no es un valor válido.");
                license.RuleFor(l => l.Number).MaximumLength(100);
                license.RuleFor(l => l.Issuer).MaximumLength(200);
                license.RuleFor(l => l.VerificationStatus)
                    .Must(v => ProfessionalOptions.IsAllowed(ProfessionalOptions.VerificationStatuses, v))
                    .WithMessage("El estado de verificación no es un valor válido.");
            });
    }
}

