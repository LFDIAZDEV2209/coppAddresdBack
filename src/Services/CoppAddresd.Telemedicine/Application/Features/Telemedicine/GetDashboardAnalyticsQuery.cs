using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Infrastructure.Cache;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>Punto diario de la serie temporal (bucket diario del dashboard).</summary>
public sealed record DailyAppointmentCountDto(DateOnly Day, int Count);

/// <summary>Distribución de citas por estado (dashboard).</summary>
public sealed record StatusCountDto(AppointmentStatus Status, int Count);

/// <summary>Distribución de citas por hora del día (franjas de mayor demanda).</summary>
public sealed record HourlyCountDto(int Hour, int Count);

/// <summary>Actividad agregada por profesional (solo vista admin global).</summary>
public sealed record ProfessionalActivityDto(
    Guid ProfessionalId,
    string? ProfessionalName,
    int Total,
    int Completed,
    int Cancelled,
    int UniquePatients
);

/// <summary>Citas del rango agrupadas por estado USA del paciente (heatmap).</summary>
public sealed record StateCountDto(string Code, int Count);

/// <summary>KPIs del dashboard de Telemedicina (varían por rol: global vs profesional).</summary>
public sealed record DashboardKpisDto(
    int TotalAppointments,
    int AppointmentsToday,
    int UpcomingAppointments,
    int Completed,
    int Cancelled,
    int NoShow,
    int Pending,
    int UniquePatients,
    int ActiveProfessionals
);

/// <summary>
/// Payload completo del dashboard de Telemedicina: KPIs, serie temporal, distribución
/// por estado, distribución horaria, actividad por profesional y próximas citas.
/// </summary>
public sealed record DashboardAnalyticsDto(
    DashboardKpisDto Kpis,
    IReadOnlyList<DailyAppointmentCountDto> DailySeries,
    IReadOnlyList<StatusCountDto> StatusDistribution,
    IReadOnlyList<HourlyCountDto> HourlyDistribution,
    IReadOnlyList<ProfessionalActivityDto> ProfessionalActivity,
    IReadOnlyList<AppointmentDto> UpcomingAppointments,
    IReadOnlyList<StateCountDto> States
);

/// <summary>
/// Consulta los datos del dashboard de Telemedicina. <paramref name="ProfessionalId"/>
/// null = vista global (admin); con valor = solo las citas de ese profesional
/// (dashboard del profesional por identidad). El rango por defecto es de 30 días.
/// </summary>
public sealed record GetDashboardAnalyticsQuery(
    Guid? ProfessionalId,
    DateTimeOffset? From,
    DateTimeOffset? To
) : IRequest<DashboardAnalyticsDto>;

public sealed class GetDashboardAnalyticsQueryHandler(
    IAppointmentRepository appointments,
    IAppointmentReferenceDataService referenceData,
    ICacheService cache
) : IRequestHandler<GetDashboardAnalyticsQuery, DashboardAnalyticsDto>
{
    private const int UpcomingLimit = 8;

    /// <summary>
    /// TTL de stats con jitter (30-60 s): el dashboard roza el presente
    /// (KPI/serie de hoy), el staleness máximo tolerado es un TTL y la clave
    /// por alcance+rango evita expiraciones sincronizadas (stampede).
    /// </summary>
    private static TimeSpan JitteredStatsTtl() =>
        TimeSpan.FromSeconds(Random.Shared.Next(30, 61));

    public async Task<DashboardAnalyticsDto> Handle(
        GetDashboardAnalyticsQuery request,
        CancellationToken ct
    )
    {
        var now = DateTimeOffset.UtcNow;

        // Clave: alcance (profesional o global) + rango SOLICITADO (null usa el
        // default de 30 días y comparte clave: staleness ≤ TTL).
        var scope = request.ProfessionalId?.ToString() ?? "global";
        var fromKey = request.From?.ToUniversalTime().ToString("o") ?? "default";
        var toKey = request.To?.ToUniversalTime().ToString("o") ?? "default";
        var cacheKey = $"stats:dashboard-analytics:{scope}:{fromKey}:{toKey}:v1";

        // Los AGREGADOS (KPIs, series, distribuciones) se cachean; las próximas
        // citas llevan nombre de paciente (PHI a nivel fila, política de caché
        // en docs/modules/cache/README.md) y se resuelven SIEMPRE en vivo,
        // nunca dentro del payload cacheado.
        var aggregates = await cache.GetOrCreateAsync(
            cacheKey,
            JitteredStatsTtl(),
            token => BuildAggregatesAsync(request, now, token),
            ct
        );

        var upcoming = await appointments.ListUpcomingAsync(
            request.ProfessionalId,
            now,
            UpcomingLimit,
            ct
        );
        var upcomingDtos = await AppointmentMapper.BuildDtosAsync(upcoming, referenceData, ct);

        return aggregates with { UpcomingAppointments = upcomingDtos };
    }

    /// <summary>
    /// Construye el payload agregado (sin próximas citas): es la unidad
    /// cacheada. En miss corre las consultas rollup-first del repositorio; en
    /// hit se evita todo su costo (la lista viva de citas va aparte).
    /// </summary>
    private async Task<DashboardAnalyticsDto> BuildAggregatesAsync(
        GetDashboardAnalyticsQuery request,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var to = (request.To ?? now).ToUniversalTime();
        var from = (request.From ?? to.AddDays(-30)).ToUniversalTime();

        var startOfToday = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var startOfTomorrow = startOfToday.AddDays(1);
        var endOfNextWeek = startOfToday.AddDays(7);

        // EF Core no permite operaciones concurrentes sobre el mismo DbContext
        // (scoped por request): las consultas se encadenan secuencialmente. Cada
        // una es un conteo/agrupación dirigida con índice, de latencia < 100ms.
        var series = await appointments.CountGroupedByDayAsync(
            request.ProfessionalId,
            from,
            to,
            ct
        );
        var statuses = await appointments.CountGroupedByStatusAsync(
            request.ProfessionalId,
            from,
            to,
            ct
        );
        var hours = await appointments.CountGroupedByHourAsync(
            request.ProfessionalId,
            from,
            to,
            ct
        );
        var patients = await appointments.CountDistinctPatientsAsync(
            request.ProfessionalId,
            from,
            to,
            ct
        );

        var total = await appointments.CountInRangeAsync(request.ProfessionalId, from, to, ct);
        var today = await appointments.CountInRangeAsync(
            request.ProfessionalId,
            startOfToday,
            startOfTomorrow,
            ct
        );
        // Rango estrecho que toca el presente: consulta directa (el bucket
        // diario no puede excluir el intradía y el To cae a medianoche).
        var upcomingRange = await appointments.CountInRangeAsync(
            request.ProfessionalId,
            now,
            endOfNextWeek,
            ct,
            usePreagg: false
        );

        // La actividad por profesional solo tiene sentido en la vista global.
        var professionalActivity = request.ProfessionalId is null
            ? await appointments.CountGroupedByProfessionalAsync(from, to, ct)
            : [];

        // Dimensión por estado USA del paciente (heatmap): una entrada por cita
        // del rango; los estados se resuelven una vez por paciente (dedup +
        // caché de referencias). Sin estado registrado → fuera del mapa.
        var rangePatientIds = await appointments.ListPatientIdsAsync(
            request.ProfessionalId,
            from,
            to,
            ct
        );
        var patientsById = await AppointmentMapper.FetchAllAsync(
            rangePatientIds.Distinct().ToList(),
            id => referenceData.GetPatientAsync(id, ct)
        );
        var states = rangePatientIds
            .Select(id => patientsById.GetValueOrDefault(id)?.StateCode?.Trim().ToUpperInvariant())
            .Where(code => !string.IsNullOrEmpty(code))
            .GroupBy(code => code!)
            .Select(g => new StateCountDto(g.Key, g.Count()))
            .OrderByDescending(s => s.Count)
            .ToList();

        // Serie diaria completa: rellena los días sin citas con 0 para que la
        // gráfica sea continua en todo el rango.
        var dailySeries = BuildContinuousDailySeries(from, to, series);

        var statusDistribution = statuses
            .Select(s => new StatusCountDto(s.Status, s.Count))
            .OrderBy(s => s.Status)
            .ToList();

        var hourlyDistribution = BuildCompleteHourlyDistribution(hours);

        var activityDtos = await BuildProfessionalActivityDtosAsync(
            professionalActivity,
            referenceData,
            ct
        );

        var kpis = new DashboardKpisDto(
            total,
            today,
            upcomingRange,
            statusDistribution.FirstOrDefault(s => s.Status == AppointmentStatus.Completed)?.Count
                ?? 0,
            statusDistribution.FirstOrDefault(s => s.Status == AppointmentStatus.Cancelled)?.Count
                ?? 0,
            statusDistribution.FirstOrDefault(s => s.Status == AppointmentStatus.NoShow)?.Count
                ?? 0,
            statusDistribution.FirstOrDefault(s => s.Status == AppointmentStatus.Confirmed)?.Count
                ?? 0,
            patients,
            request.ProfessionalId is null
                ? await CountActiveProfessionalsAsync(appointments, from, to, ct)
                : 0
        );

        return new DashboardAnalyticsDto(
            kpis,
            dailySeries,
            statusDistribution,
            hourlyDistribution,
            activityDtos,
            [], // próximas citas: SIEMPRE en vivo (PHI), ver Handle
            states
        );
    }

    /// <summary>Rellena los días sin citas del rango con 0 (gráfica continua).</summary>
    private static IReadOnlyList<DailyAppointmentCountDto> BuildContinuousDailySeries(
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<DailyAppointmentCount> counts
    )
    {
        var byDay = counts.ToDictionary(c => DateOnly.FromDateTime(c.Day.UtcDateTime));
        var days = (int)(to.Date - from.Date).TotalDays;
        var result = new List<DailyAppointmentCountDto>(days + 1);

        for (var i = 0; i <= days; i++)
        {
            var day = DateOnly.FromDateTime(from.Date.AddDays(i));
            result.Add(new DailyAppointmentCountDto(day, byDay.GetValueOrDefault(day)?.Count ?? 0));
        }

        return result;
    }

    /// <summary>Profesionales con citas en el rango (KPI de la vista global).</summary>
    private static async Task<int> CountActiveProfessionalsAsync(
        IAppointmentRepository appointments,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct
    ) => await appointments.CountDistinctProfessionalsAsync(from, to, ct);

    /// <summary>Completa las 24 horas del día con 0 (evita huecos en el eje X).</summary>
    private static IReadOnlyList<HourlyCountDto> BuildCompleteHourlyDistribution(
        IReadOnlyList<HourlyAppointmentCount> counts
    )
    {
        var byHour = counts.ToDictionary(c => c.Hour);
        return Enumerable
            .Range(0, 24)
            .Select(hour => new HourlyCountDto(hour, byHour.GetValueOrDefault(hour)?.Count ?? 0))
            .ToList();
    }

    /// <summary>Resuelve los nombres de los profesionales con una sola pasada deduplicada (sin N+1).</summary>
    private static async Task<
        IReadOnlyList<ProfessionalActivityDto>
    > BuildProfessionalActivityDtosAsync(
        IReadOnlyList<ProfessionalAppointmentActivity> items,
        IAppointmentReferenceDataService referenceData,
        CancellationToken ct
    )
    {
        var ids = items.Select(i => i.ProfessionalId).Distinct().ToList();
        // Fan-out paralelo: en frío cada referencia es un HTTP al backend;
        // secuencial costaba N×latencia (3.8s medidos con 14 profesionales).
        var names = await AppointmentMapper.FetchAllAsync(
            ids,
            id => referenceData.GetProfessionalAsync(id, ct)
        );

        return items
            .Select(i => new ProfessionalActivityDto(
                i.ProfessionalId,
                names.GetValueOrDefault(i.ProfessionalId)?.FullName,
                i.Total,
                i.Completed,
                i.Cancelled,
                i.UniquePatients
            ))
            .ToList();
    }
}
