using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Ranura de agendamiento disponible. Los tiempos están en UTC; la UI convierte
/// a la hora local con <c>TimezoneOffset</c> del resultado. Por veredicto B4
/// (REQ-TELE-01) la respuesta contiene únicamente slots libres
/// (<c>IsAvailable = true</c>, <c>ConflictReason = null</c>); los campos se
/// conservan por compatibilidad contractual con ERP/App.
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
            var slots = AvailabilitySlotBuilder
                .BuildProfessionalSlots(
                    schedules.Where(s => s.Weekday == isoWeekday),
                    dayAppointments,
                    dayStart,
                    settings.DefaultAppointmentDurationMinutes,
                    now.AddHours(settings.MinAdvanceBookingHours)
                )
                // Veredicto B4 (REQ-TELE-01 prevalece): el endpoint devuelve
                // únicamente slots disponibles; el filtrado ocurre en el servidor.
                .Where(s => s.IsAvailable)
                .ToList();
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

        // Una sola query de citas para todos los candidatos con horario ese día
        // (evita N+1): mismo criterio de ventana que el modo rango y el
        // endpoint availability/professionals.
        var activeAppointments = await appointments.ListByProfessionalsAsync(
            withSchedule.Select(x => x.Candidate.ProfessionalId).Distinct().ToList(),
            dayStart.AddMinutes(-SchedulingRules.MaxDurationMinutes),
            dayStart.AddDays(1),
            ct
        );
        var appointmentsByProfessional = activeAppointments
            .Where(a =>
                a.Status
                    is AppointmentStatus.Requested
                        or AppointmentStatus.Confirmed
                        or AppointmentStatus.InProgress
            )
            .GroupBy(a => a.ProfessionalId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Domain.Entities.Appointment>)g.ToList());

        var slotsByStart =
            new Dictionary<(DateTimeOffset Start, DateTimeOffset End), HashSet<Guid>>();
        foreach (var entry in withSchedule)
        {
            var freeStarts = AvailabilitySlotBuilder.FreeSlotKeys(
                entry.DaySchedules,
                appointmentsByProfessional.GetValueOrDefault(entry.Candidate.ProfessionalId)
                    ?? (IReadOnlyList<Domain.Entities.Appointment>)[],
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

        // Veredicto B4 (REQ-TELE-01 prevalece): solo slots con al menos un
        // profesional libre; los ocupados o en anticipación no se devuelven.
        var result = slotsByStart
            .OrderBy(kv => kv.Key.Start)
            .Select(kv => new AvailabilitySlotDto(
                kv.Key.Start,
                kv.Key.End,
                settings.DefaultAppointmentDurationMinutes,
                true,
                null,
                kv.Value.Count
            ))
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

/// <summary>Disponibilidad de un día dentro de un rango (slots ya filtrados B4).</summary>
public sealed record AvailabilityDayDto(DateOnly Date, IReadOnlyList<AvailabilitySlotDto> Slots);

/// <summary>
/// Disponibilidad de un RANGO en una sola llamada (modo profesional o
/// especialidad): <c>days</c> incluye todos los días del rango, con
/// <c>slots</c> vacíos cuando el día no tiene cupo. Mismas reglas B4 y de
/// anticipación que el modo de un día.
/// </summary>
public sealed record AvailabilityRangeResult(
    Guid? ProfessionalId,
    Guid? SpecialtyId,
    string TimezoneOffset,
    IReadOnlyList<AvailabilityDayDto> Days
);

/// <summary>
/// Calcula los slots libres de un rango de fechas (máx. 14 días) cruzando el
/// horario semanal de los profesionales contra las citas activas del rango y
/// las reglas de <c>TelemedicineSettings</c>. Con batching obligatorio: 1
/// consulta de candidatos (modo especialidad) + 1 consulta de citas para todo
/// el rango; el cálculo de slots es en memoria — nunca 1 query por día ni por
/// profesional-día.
/// </summary>
public sealed record GetAvailabilityRangeSlotsQuery(
    Guid? ProfessionalId,
    Guid? SpecialtyId,
    Guid? OrganizationId,
    Guid? ClinicId,
    Guid? LocationId,
    DateOnly From,
    DateOnly To
) : IRequest<AvailabilityRangeResult>;

public sealed class GetAvailabilityRangeSlotsQueryValidator
    : AbstractValidator<GetAvailabilityRangeSlotsQuery>
{
    /// <summary>Tope del rango: 14 días inclusive (horizonte del wizard de la app).</summary>
    public const int MaxRangeDays = 14;

    public GetAvailabilityRangeSlotsQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => x.ProfessionalId.HasValue || x.SpecialtyId.HasValue)
            .WithMessage("Se requiere ProfessionalId o SpecialtyId.");
        RuleFor(x => x)
            .Must(x => x.ProfessionalId.HasValue || x.OrganizationId.HasValue)
            .WithMessage("El modo por especialidad requiere OrganizationId.");
        RuleFor(x => x.From)
            .Must(d => d != default)
            .WithMessage("La fecha 'from' es requerida (formato YYYY-MM-DD).");
        RuleFor(x => x.To)
            .Must(d => d != default)
            .WithMessage("La fecha 'to' es requerida (formato YYYY-MM-DD).");
        RuleFor(x => x)
            .Must(x => x.To >= x.From)
            .WithMessage("El rango es inválido: 'to' no puede ser anterior a 'from'.");
        RuleFor(x => x)
            .Must(x => x.To.DayNumber - x.From.DayNumber < MaxRangeDays)
            .WithMessage($"El rango máximo es {MaxRangeDays} días.");
    }
}

public sealed class GetAvailabilityRangeSlotsQueryHandler(
    IAppointmentRepository appointments,
    IAppointmentReferenceDataService referenceData,
    ITelemedicineSettingsProvider settingsProvider
) : IRequestHandler<GetAvailabilityRangeSlotsQuery, AvailabilityRangeResult>
{
    /// <summary>Las franjas <c>HH:mm</c> del ERP se interpretan en UTC del día pedido.</summary>
    private const string UtcOffset = "+00:00";

    public async Task<AvailabilityRangeResult> Handle(
        GetAvailabilityRangeSlotsQuery request,
        CancellationToken ct
    )
    {
        var now = DateTimeOffset.UtcNow;
        var settings = await settingsProvider.GetSettingsAsync(
            request.OrganizationId ?? Guid.Empty,
            request.ClinicId,
            ct
        );

        var from = request.From;
        var to = request.To;
        // Rango absoluto de citas: ventana completa, con margen hacia atrás por
        // la duración máxima de slot (una cita del día anterior puede
        // solaparse con los primeros slots de 'from').
        var rangeEndUtc = new DateTimeOffset(
            to.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        ).AddDays(1);
        var days = new List<AvailabilityDayDto>(to.DayNumber - from.DayNumber + 1);

        if (request.ProfessionalId is { } professionalId)
        {
            await ReferenceDataGuard.RequireProfessionalAsync(referenceData, professionalId, ct);
            var schedules = await referenceData.GetProfessionalSchedulesAsync(professionalId, ct);
            var rangeAppointments = await appointments.ListByProfessionalAsync(
                professionalId,
                new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).AddMinutes(
                    -SchedulingRules.MaxDurationMinutes
                ),
                rangeEndUtc,
                ct
            );
            var active = rangeAppointments
                .Where(a =>
                    a.Status
                        is AppointmentStatus.Requested
                            or AppointmentStatus.Confirmed
                            or AppointmentStatus.InProgress
                )
                .ToList();

            for (var date = from; date <= to; date = date.AddDays(1))
            {
                days.Add(
                    new AvailabilityDayDto(
                        date,
                        SlotsDelProfesionalEn(
                            schedules,
                            date,
                            active,
                            settings.DefaultAppointmentDurationMinutes,
                            now.AddHours(settings.MinAdvanceBookingHours)
                        )
                    )
                );
            }
            return new AvailabilityRangeResult(professionalId, null, UtcOffset, days);
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

        // Aunque no haya candidatos, todos los días del rango aparecen con
        // slots vacíos (contrato: days[] completo).
        if (candidates.Count == 0)
        {
            for (var date = from; date <= to; date = date.AddDays(1))
            {
                days.Add(new AvailabilityDayDto(date, []));
            }
            return new AvailabilityRangeResult(null, specialtyId, UtcOffset, days);
        }

        var minStart = now.AddHours(settings.MinAdvanceBookingHours);
        var rangeStartUtc = new DateTimeOffset(
            from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        );
        // Una sola query de citas para todo el rango y todos los candidatos
        // (evita N+1); el conteo por slot se resuelve en memoria.
        var batchedAppointments = await appointments.ListByProfessionalsAsync(
            candidates.Select(c => c.ProfessionalId).Distinct().ToList(),
            rangeStartUtc.AddMinutes(-SchedulingRules.MaxDurationMinutes),
            rangeEndUtc,
            ct
        );
        var activeByProfessional = batchedAppointments
            .Where(a =>
                a.Status
                    is AppointmentStatus.Requested
                        or AppointmentStatus.Confirmed
                        or AppointmentStatus.InProgress
            )
            .GroupBy(a => a.ProfessionalId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Domain.Entities.Appointment>)g.ToList());

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var isoWeekday = ((int)date.DayOfWeek + 6) % 7 + 1;
            var dayStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            var slotsByStart =
                new Dictionary<(DateTimeOffset Start, DateTimeOffset End), HashSet<Guid>>();
            foreach (var candidate in candidates)
            {
                var daySchedules = candidate.Schedules.Where(s => s.Weekday == isoWeekday).ToList();
                if (daySchedules.Count == 0)
                {
                    continue;
                }

                var dayAppointments =
                    activeByProfessional.GetValueOrDefault(candidate.ProfessionalId)
                    ?? (IReadOnlyList<Domain.Entities.Appointment>)[];
                foreach (
                    var key in AvailabilitySlotBuilder.FreeSlotKeys(
                        daySchedules,
                        dayAppointments,
                        dayStart,
                        settings.DefaultAppointmentDurationMinutes,
                        minStart
                    )
                )
                {
                    if (!slotsByStart.TryGetValue(key, out var set))
                    {
                        set = [];
                        slotsByStart[key] = set;
                    }
                    set.Add(candidate.ProfessionalId);
                }
            }

            days.Add(
                new AvailabilityDayDto(
                    date,
                    slotsByStart
                        .OrderBy(kv => kv.Key.Start)
                        .Select(kv => new AvailabilitySlotDto(
                            kv.Key.Start,
                            kv.Key.End,
                            settings.DefaultAppointmentDurationMinutes,
                            true,
                            null,
                            kv.Value.Count
                        ))
                        .ToList()
                )
            );
        }

        return new AvailabilityRangeResult(null, specialtyId, UtcOffset, days);
    }

    /// <summary>Slots libres del profesional en un día del rango (regla B4: solo libres).</summary>
    private static List<AvailabilitySlotDto> SlotsDelProfesionalEn(
        IReadOnlyList<ProfessionalScheduleRefDto> schedules,
        DateOnly date,
        IReadOnlyList<Domain.Entities.Appointment> activeAppointments,
        int durationMinutes,
        DateTimeOffset minStart
    ) =>
        AvailabilitySlotBuilder
            .BuildProfessionalSlots(
                schedules.Where(s => s.Weekday == IsoWeekdayOf(date)),
                activeAppointments,
                new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
                durationMinutes,
                minStart
            )
            .Where(s => s.IsAvailable)
            .ToList();

    private static int IsoWeekdayOf(DateOnly date) => ((int)date.DayOfWeek + 6) % 7 + 1;
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
