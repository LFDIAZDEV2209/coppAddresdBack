using FluentValidation;

namespace CoppAddresd.Application.Features.Professionals;

/// <summary>Validación de comandos de la estructura organizacional.</summary>
public sealed class OrganizationCommandValidators
{
    public sealed class CreateOrganizationValidator : AbstractValidator<CreateOrganizationCommand>
    {
        public CreateOrganizationValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("El código es requerido.")
                .MaximumLength(50)
                .Matches("^[a-z0-9_-]+$").WithMessage(
                    "El código solo admite minúsculas, números, guiones y guiones bajos.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("El nombre es requerido.")
                .MaximumLength(200);
        }
    }

    public sealed class UpdateOrganizationValidator : AbstractValidator<UpdateOrganizationCommand>
    {
        public UpdateOrganizationValidator()
        {
            RuleFor(x => x.Name).MaximumLength(200);
        }
    }

    public sealed class CreateClinicValidator : AbstractValidator<CreateClinicCommand>
    {
        public CreateClinicValidator()
        {
            RuleFor(x => x.OrganizationId).NotEmpty();
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("El nombre es requerido.")
                .MaximumLength(200);
            RuleFor(x => x.Code).MaximumLength(50);
        }
    }

    public sealed class UpdateClinicValidator : AbstractValidator<UpdateClinicCommand>
    {
        public UpdateClinicValidator()
        {
            RuleFor(x => x.Name).MaximumLength(200);
            RuleFor(x => x.Code).MaximumLength(50);
        }
    }

    public sealed class CreateLocationValidator : AbstractValidator<CreateLocationCommand>
    {
        public CreateLocationValidator()
        {
            RuleFor(x => x.ClinicId).NotEmpty();
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("El nombre es requerido.")
                .MaximumLength(200);
            RuleFor(x => x.AddressLine1).MaximumLength(200);
            RuleFor(x => x.AddressLine2).MaximumLength(200);
            RuleFor(x => x.PostalCode).MaximumLength(10);
            RuleFor(x => x.PhoneCountryCode).MaximumLength(10);
            RuleFor(x => x.PhoneNumber).MaximumLength(20);
        }
    }

    public sealed class UpdateLocationValidator : AbstractValidator<UpdateLocationCommand>
    {
        public UpdateLocationValidator()
        {
            RuleFor(x => x.Name).MaximumLength(200);
            RuleFor(x => x.AddressLine1).MaximumLength(200);
            RuleFor(x => x.AddressLine2).MaximumLength(200);
            RuleFor(x => x.PostalCode).MaximumLength(10);
            RuleFor(x => x.PhoneCountryCode).MaximumLength(10);
            RuleFor(x => x.PhoneNumber).MaximumLength(20);
        }
    }
}
