using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetCalendar;

/// <summary>
/// Consulta de rollups diarios para una ventana (SPEC §7.3, máximo 92 días).
/// <c>from</c>/<c>to</c> son fechas locales del paciente. La inscripción no
/// existe → 404 (lo lanza el repositorio).
/// </summary>
public sealed record GetCalendarQuery(
    Guid EnrollmentId,
    DateOnly From,
    DateOnly To) : IRequest<ProgramCalendarDto>;

/// <summary>Validación de la ventana del calendario (máx 92 días, SPEC §7.3).</summary>
public sealed class GetCalendarQueryValidator : AbstractValidator<GetCalendarQuery>
{
    private const int MaxWindowDays = 92;

    public GetCalendarQueryValidator()
    {
        RuleFor(x => x.EnrollmentId)
            .NotEmpty()
            .WithMessage("El enrollmentId es requerido.");

        RuleFor(x => x.From)
            .NotEmpty()
            .WithMessage("La fecha desde es requerida.");

        RuleFor(x => x.To)
            .NotEmpty()
            .WithMessage("La fecha hasta es requerida.");

        RuleFor(x => x)
            .Must(x => x.To >= x.From)
            .WithMessage("La fecha 'hasta' debe ser mayor o igual a 'desde'.")
            .Must(x => x.To.DayNumber - x.From.DayNumber + 1 <= MaxWindowDays)
            .WithMessage($"La ventana del calendario no puede superar {MaxWindowDays} días.");
    }
}

/// <summary>Delega en el repositorio; la proyección ya viene con el shape §7.3.</summary>
public sealed class GetCalendarQueryHandler(
    IProgramRepository repository) : IRequestHandler<GetCalendarQuery, ProgramCalendarDto>
{
    public async Task<ProgramCalendarDto> Handle(GetCalendarQuery request, CancellationToken ct)
        => await repository.GetCalendarAsync(request.EnrollmentId, request.From, request.To, ct);
}