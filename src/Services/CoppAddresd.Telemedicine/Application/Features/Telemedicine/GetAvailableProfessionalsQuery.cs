using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Profesional con disponibilidad dentro de la ventana consultada.
/// <c>NextAvailableStart</c> es la primera ranura libre (UTC) y
/// <c>AvailableDays</c> cuántos días de la ventana tienen al menos una.
/// Solo ids: el nombre y demás identidad viven en el catálogo de profesionales.
/// </summary>
public sealed record AvailableProfessionalDto(
    Guid ProfessionalId,
    DateTimeOffset NextAvailableStart,
    int AvailableDays
);

/// <summary>
/// Profesionales de una especialidad con al menos una ranura libre dentro de
/// una ventana de días. La app móvil lo usa para marcar "Con cupo" en el picker
/// de profesional sin consultar día por día (el picker vive antes de elegir
/// fecha). Mismo horizonte de reloj y reglas que
/// <see cref="GetAvailabilitySlotsQuery"/>.
/// </summary>
public sealed record AvailableProfessionalsResult(
    Guid SpecialtyId,
    DateOnly From,
    DateOnly To,
    string TimezoneOffset,
    IReadOnlyList<AvailableProfessionalDto> Professionals
);

public sealed record GetAvailableProfessionalsQuery(
    Guid SpecialtyId,
    Guid? OrganizationId,
    Guid? ClinicId,
    Guid? LocationId,
    DateOnly From,
    DateOnly To
) : IRequest<AvailableProfessionalsResult>;

public sealed class GetAvailableProfessionalsQueryValidator
    : AbstractValidator<GetAvailableProfessionalsQuery>
{
    /// <summary>Tope de ventana (una sola query de citas por rango, sin N+1).</summary>
    public const int MaxWindowDays = 31;

    public GetAvailableProfessionalsQueryValidator()
    {
        RuleFor(x => x.SpecialtyId).NotEmpty().WithMessage("La especialidad es requerida.");
        RuleFor(x => x.OrganizationId)
            .NotEmpty()
            .WithMessage("El modo por especialidad requiere OrganizationId.");
        RuleFor(x => x.From)
            .Must(d => d != default)
            .WithMessage("La fecha inicial es requerida (formato YYYY-MM-DD).");
        RuleFor(x => x.To)
            .Must(d => d != default)
            .WithMessage("La fecha final es requerida (formato YYYY-MM-DD).");
        RuleFor(x => x)
            .Must(x => x.To >= x.From)
            .WithMessage("La fecha final no puede ser anterior a la inicial.");
        RuleFor(x => x)
            .Must(x => x.To.DayNumber - x.From.DayNumber < MaxWindowDays)
            .WithMessage($"La ventana máxima es {MaxWindowDays} días.");
    }
}

public sealed class GetAvailableProfessionalsQueryHandler(
    IAppointmentRepository appointments,
    IAppointmentReferenceDataService referenceData,
    ITelemedicineSettingsProvider settingsProvider
) : IRequestHandler<GetAvailableProfessionalsQuery, AvailableProfessionalsResult>
{
    /// <summary>Las franjas <c>HH:mm</c> del ERP se interpretan en UTC del día pedido.</summary>
    private const string UtcOffset = "+00:00";

    public async Task<AvailableProfessionalsResult> Handle(
        GetAvailableProfessionalsQuery request,
        CancellationToken ct
    )
    {
        var now = DateTimeOffset.UtcNow;
        var settings = await settingsProvider.GetSettingsAsync(
            request.OrganizationId ?? Guid.Empty,
            request.ClinicId,
            ct
        );

        await ReferenceDataGuard.RequireSpecialtyAsync(referenceData, request.SpecialtyId, ct);
        var candidates =
            await referenceData.GetProfessionalCandidatesAsync(
                request.SpecialtyId,
                request.OrganizationId,
                request.ClinicId,
                request.LocationId,
                ct
            ) ?? throw new NotFoundException("Especialidad", request.SpecialtyId);

        if (candidates.Count == 0)
        {
            return new AvailableProfessionalsResult(
                request.SpecialtyId,
                request.From,
                request.To,
                UtcOffset,
                []
            );
        }

        var windowStart = new DateTimeOffset(
            request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        );
        var windowEnd = new DateTimeOffset(
            request.To.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        ).AddDays(1);
        var minStart = now.AddHours(settings.MinAdvanceBookingHours);

        // Una sola query de citas para todos los candidatos (evita N+1): el
        // rango cubre la ventana completa con el margen de duración máxima
        // hacia atrás de la primera fecha, igual que el modo especialidad de
        // /availability.
        var windowAppointments = await appointments.ListByProfessionalsAsync(
            candidates.Select(c => c.ProfessionalId).ToList(),
            windowStart.AddMinutes(-SchedulingRules.MaxDurationMinutes),
            windowEnd,
            ct
        );
        var activeByProfessional = windowAppointments
            .Where(a =>
                a.Status
                    is AppointmentStatus.Requested
                        or AppointmentStatus.Confirmed
                        or AppointmentStatus.InProgress
            )
            .GroupBy(a => a.ProfessionalId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Domain.Entities.Appointment>)g.ToList());

        var availability = new Dictionary<Guid, (DateTimeOffset Next, int Days)>();
        for (var date = request.From; date <= request.To; date = date.AddDays(1))
        {
            var isoWeekday = ((int)date.DayOfWeek + 6) % 7 + 1;
            var dayStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            foreach (var candidate in candidates)
            {
                var daySchedules = candidate.Schedules.Where(s => s.Weekday == isoWeekday).ToList();
                if (daySchedules.Count == 0)
                {
                    continue;
                }

                var appointmentsForDay =
                    activeByProfessional.GetValueOrDefault(candidate.ProfessionalId)
                    ?? (IReadOnlyList<Domain.Entities.Appointment>)[];
                var firstFree = AvailabilitySlotBuilder
                    .FreeSlotKeys(
                        daySchedules,
                        appointmentsForDay,
                        dayStart,
                        settings.DefaultAppointmentDurationMinutes,
                        minStart
                    )
                    .FirstOrDefault();
                if (firstFree.Start == default)
                {
                    continue;
                }

                availability[candidate.ProfessionalId] = availability.TryGetValue(
                    candidate.ProfessionalId,
                    out var current
                )
                    ? (current.Next, current.Days + 1)
                    : (firstFree.Start, 1);
            }
        }

        var professionals = availability
            .OrderBy(kv => kv.Value.Next)
            .ThenBy(kv => kv.Key)
            .Select(kv => new AvailableProfessionalDto(kv.Key, kv.Value.Next, kv.Value.Days))
            .ToList();

        return new AvailableProfessionalsResult(
            request.SpecialtyId,
            request.From,
            request.To,
            UtcOffset,
            professionals
        );
    }
}
