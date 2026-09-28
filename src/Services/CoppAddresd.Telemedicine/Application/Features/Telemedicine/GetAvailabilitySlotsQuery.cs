using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Ranura de agendamiento con su estado de ocupación. Los tiempos están en UTC;
/// la UI convierte a la hora local con <c>TimezoneOffset</c> del resultado.
/// </summary>
public sealed record AvailabilitySlotDto(
    DateTimeOffset Start,
    DateTimeOffset End,
    int DurationMinutes,
    bool IsAvailable,
    string? ConflictReason,
    int AvailableProfessionalCount
);

/// <summary>
/// Disponibilidad de un día: en modo profesional los slots son del profesional
/// pedido; en modo especialidad es la unión de slots de los profesionales
/// elegibles con el conteo de libres por slot (sin asignar ni reservar).
/// </summary>
public sealed record AvailabilitySlotsResult(
    Guid? ProfessionalId,
    Guid? SpecialtyId,
    DateOnly Date,
    string TimezoneOffset,
    IReadOnlyList<AvailabilitySlotDto> Slots
);

/// <summary>
/// Calcula los slots libres de un día cruzando el horario semanal del
/// profesional (<c>erp.professional_schedules</c>, vía datos de referencia)
/// contra las citas activas (<c>Requested</c>, <c>Confirmed</c>,
/// <c>InProgress</c>) y las reglas de <c>TelemedicineSettings</c>.
/// Modo profesional (<c>ProfessionalId</c>) o modo especialidad
/// (<c>SpecialtyId</c> + <c>OrganizationId</c> requerido).
/// </summary>
public sealed record GetAvailabilitySlotsQuery(
    Guid? ProfessionalId,
    Guid? SpecialtyId,
    Guid? OrganizationId,
    Guid? ClinicId,
    Guid? LocationId,
    DateOnly Date
) : IRequest<AvailabilitySlotsResult>;

public sealed class GetAvailabilitySlotsQueryValidator
    : AbstractValidator<GetAvailabilitySlotsQuery>
{
    public GetAvailabilitySlotsQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => x.ProfessionalId.HasValue || x.SpecialtyId.HasValue)
            .WithMessage("Se requiere ProfessionalId o SpecialtyId.");
        RuleFor(x => x)
            .Must(x => x.ProfessionalId.HasValue || x.OrganizationId.HasValue)
            .WithMessage("El modo por especialidad requiere OrganizationId.");
        RuleFor(x => x.Date)
            .Must(d => d != default)
            .WithMessage("La fecha es requerida (formato YYYY-MM-DD).");
    }
}

public sealed class GetAvailabilitySlotsQueryHandler(
    IAppointmentRepository appointments,
    IAppointmentReferenceDataService referenceData,
    ITelemedicineSettingsProvider settingsProvider
) : IRequestHandler<GetAvailabilitySlotsQuery, AvailabilitySlotsResult>
{
    /// <summary>Las franjas <c>HH:mm</c> del ERP se interpretan en UTC del día pedido.</summary>
    private const string UtcOffset = "+00:00";

    public async Task<AvailabilitySlotsResult> Handle(
        GetAvailabilitySlotsQuery request,
        CancellationToken ct
    )
    {
        var now = DateTimeOffset.UtcNow;
        var organizationId = request.OrganizationId ?? Guid.Empty;
        var settings = await settingsProvider.GetSettingsAsync(
            organizationId,
            request.ClinicId,
            ct
        );

        var dayStart = new DateTimeOffset(
            request.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        );
        var isoWeekday = ((int)request.Date.DayOfWeek + 6) % 7 + 1;

        if (request.ProfessionalId is { } professionalId)
        {
            await ReferenceDataGuard.RequireProfessionalAsync(referenceData, professionalId, ct);
            var schedules = await referenceData.GetProfessionalSchedulesAsync(professionalId, ct);
            var dayAppointments = await ActiveAppointmentsOnDayAsync(professionalId, dayStart, ct);
            var slots = AvailabilitySlotBuilder.BuildProfessionalSlots(
                schedules.Where(s => s.Weekday == isoWeekday),
                dayAppointments,
                dayStart,
                settings.DefaultAppointmentDurationMinutes,
                now.AddHours(settings.MinAdvanceBookingHours)
            );
            return new AvailabilitySlotsResult(
                professionalId,
                null,
                request.Date,
                UtcOffset,
                slots
            );
        }

        var specialtyId = request.SpecialtyId!.Value;
        await ReferenceDataGuard.RequireSpecialtyAsync(referenceData, specialtyId, ct);
        var candidates =
            await referenceData.GetProfessionalCandidatesAsync(
                specialtyId,
                request.OrganizationId,
                request.ClinicId,
                request.LocationId,
                ct
            ) ?? throw new NotFoundException("Especialidad", specialtyId);

        var withSchedule = candidates
            .Select(c =>
                (
                    Candidate: c,
                    DaySchedules: c.Schedules.Where(s => s.Weekday == isoWeekday).ToList()
                )
            )
            .Where(x => x.DaySchedules.Count > 0)
            .ToList();

        if (withSchedule.Count == 0)
        {
            return new AvailabilitySlotsResult(null, specialtyId, request.Date, UtcOffset, []);
        }

        var appointmentsByProfessional =
            new Dictionary<Guid, IReadOnlyList<Domain.Entities.Appointment>>();
        foreach (var entry in withSchedule)
        {
            appointmentsByProfessional[entry.Candidate.ProfessionalId] =
                await ActiveAppointmentsOnDayAsync(entry.Candidate.ProfessionalId, dayStart, ct);
        }

        var slotsByStart =
            new Dictionary<(DateTimeOffset Start, DateTimeOffset End), HashSet<Guid>>();
        foreach (var entry in withSchedule)
        {
            var freeStarts = AvailabilitySlotBuilder.FreeSlotKeys(
                entry.DaySchedules,
                appointmentsByProfessional[entry.Candidate.ProfessionalId],
                dayStart,
                settings.DefaultAppointmentDurationMinutes,
                now.AddHours(settings.MinAdvanceBookingHours)
            );
            foreach (var key in freeStarts)
            {
                if (!slotsByStart.TryGetValue(key, out var set))
                {
                    set = [];
                    slotsByStart[key] = set;
                }
                set.Add(entry.Candidate.ProfessionalId);
            }
        }

        var allKeys = AvailabilitySlotBuilder.AllSlotKeys(
            withSchedule.SelectMany(x => x.DaySchedules),
            dayStart,
            settings.DefaultAppointmentDurationMinutes
        );

        var result = allKeys
            .OrderBy(k => k.Start)
            .Select(k =>
            {
                var freeCount = slotsByStart.TryGetValue(k, out var free) ? free.Count : 0;
                string? reason = null;
                if (freeCount == 0)
                {
                    reason =
                        k.Start < now.AddHours(settings.MinAdvanceBookingHours)
                            ? "TooSoon"
                            : "Booked";
                }
                return new AvailabilitySlotDto(
                    k.Start,
                    k.End,
                    settings.DefaultAppointmentDurationMinutes,
                    freeCount > 0,
                    reason,
                    freeCount
                );
            })
            .ToList();

        return new AvailabilitySlotsResult(null, specialtyId, request.Date, UtcOffset, result);
    }

    /// <summary>
    /// Citas activas que pisan el día (ventana ampliada hacia atrás por la
    /// duración máxima de slot: una cita iniciada el día anterior puede
    /// solaparse con los primeros slots).
    /// </summary>
    private async Task<IReadOnlyList<Domain.Entities.Appointment>> ActiveAppointmentsOnDayAsync(
        Guid professionalId,
        DateTimeOffset dayStart,
        CancellationToken ct
    )
    {
        var items = await appointments.ListByProfessionalAsync(
            professionalId,
            dayStart.AddMinutes(-SchedulingRules.MaxDurationMinutes),
            dayStart.AddDays(1),
            ct
        );
        return items
            .Where(a =>
                a.Status
                    is AppointmentStatus.Requested
                        or AppointmentStatus.Confirmed
                        or AppointmentStatus.InProgress
            )
            .ToList();
    }
}

/// <summary>
/// Construcción de slots a partir de franjas semanales: división en bloques de
/// la duración configurada, marcado de anticipación mínima y solapamiento.
/// </summary>
internal static class AvailabilitySlotBuilder
{
    public static IReadOnlyList<AvailabilitySlotDto> BuildProfessionalSlots(
        IEnumerable<Application.ReferenceData.ProfessionalScheduleRefDto> daySchedules,
        IReadOnlyList<Domain.Entities.Appointment> activeAppointments,
        DateTimeOffset dayStart,
        int durationMinutes,
        DateTimeOffset minStart
    ) =>
        AllSlotKeys(daySchedules, dayStart, durationMinutes)
            .OrderBy(k => k.Start)
            .Select(k =>
            {
                if (k.Start < minStart)
                {
                    return new AvailabilitySlotDto(
                        k.Start,
                        k.End,
                        durationMinutes,
                        false,
                        "TooSoon",
                        0
                    );
                }

                var booked = activeAppointments.Any(a =>
                    a.ScheduledStart < k.End && a.ScheduledEnd > k.Start
                );
                return booked
                    ? new AvailabilitySlotDto(k.Start, k.End, durationMinutes, false, "Booked", 0)
                    : new AvailabilitySlotDto(k.Start, k.End, durationMinutes, true, null, 1);
            })
            .ToList();

    /// <summary>Inicios libres (sin anticipación vencida ni solapamiento).</summary>
    public static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> FreeSlotKeys(
        IEnumerable<Application.ReferenceData.ProfessionalScheduleRefDto> daySchedules,
        IReadOnlyList<Domain.Entities.Appointment> activeAppointments,
        DateTimeOffset dayStart,
        int durationMinutes,
        DateTimeOffset minStart
    )
    {
        foreach (var key in AllSlotKeys(daySchedules, dayStart, durationMinutes))
        {
            if (key.Start < minStart)
            {
                continue;
            }

            if (
                activeAppointments.Any(a =>
                    a.ScheduledStart < key.End && a.ScheduledEnd > key.Start
                )
            )
            {
                continue;
            }

            yield return key;
        }
    }

    public static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> AllSlotKeys(
        IEnumerable<Application.ReferenceData.ProfessionalScheduleRefDto> daySchedules,
        DateTimeOffset dayStart,
        int durationMinutes
    )
    {
        if (durationMinutes <= 0)
        {
            yield break;
        }

        var seen = new HashSet<(DateTimeOffset Start, DateTimeOffset End)>();
        foreach (var schedule in daySchedules)
        {
            if (
                !TimeOnly.TryParse(schedule.StartTime, out var start)
                || !TimeOnly.TryParse(schedule.EndTime, out var end)
                || end <= start
            )
            {
                continue;
            }

            var cursor = dayStart.Add(start.ToTimeSpan());
            var limit = dayStart.Add(end.ToTimeSpan());
            while (cursor.AddMinutes(durationMinutes) <= limit)
            {
                var key = (Start: cursor, End: cursor.AddMinutes(durationMinutes));
                if (seen.Add(key))
                {
                    yield return key;
                }
                cursor = key.End;
            }
        }
    }
}
