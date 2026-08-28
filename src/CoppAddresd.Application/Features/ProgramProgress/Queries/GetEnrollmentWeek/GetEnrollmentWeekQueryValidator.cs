using FluentValidation;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetEnrollmentWeek;

/// <summary>
/// Validación de <see cref="GetEnrollmentWeekQuery"/>.
/// </summary>
public sealed class GetEnrollmentWeekQueryValidator : AbstractValidator<GetEnrollmentWeekQuery>
{
    public GetEnrollmentWeekQueryValidator()
    {
        RuleFor(x => x.EnrollmentId)
            .NotEmpty()
            .WithMessage("El enrollmentId es requerido.");

        RuleFor(x => x.WeekNumber)
            .GreaterThan(0)
            .WithMessage("El weekNumber debe ser mayor a 0.");

        RuleFor(x => x.ClinicianUserId)
            .NotEmpty()
            .WithMessage("El clinicianUserId es requerido.");
    }
}
