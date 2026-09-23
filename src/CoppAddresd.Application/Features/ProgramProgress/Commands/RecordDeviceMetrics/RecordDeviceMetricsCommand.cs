using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.RecordDeviceMetrics;

// Límites defensivos: un wearable no puede producir valores fuera de estos
// rangos; el objetivo es descartar bytes mal decodificados, no fijar metas.
public static class DeviceMetricLimits
{
    public const decimal MaxSteps = 200_000m;
    public const decimal MaxDistanceM = 500_000m;
    public const decimal MaxActivityKcal = 20_000m;
    public const decimal MaxSleepMinutes = 24 * 60m;
}

/// <summary>Payload del móvil con las métricas del día del anillo.</summary>
public sealed record RecordDeviceMetricsRequest(
    decimal? Steps,
    decimal? DistanceM,
    decimal? ActivityKcal,
    decimal? SleepMinutes,
    DateTime? RecordedAt
);

/// <summary>Resultado: fecha local y códigos realmente persistidos.</summary>
public sealed record RecordedDeviceMetricsDto(
    DateOnly Date,
    IReadOnlyList<string> Codes,
    DateTime ObservedAt
);

/// <summary>
/// Identidad resuelta por la API (paciente + inscripción activa + actor), nunca
/// vinculada al body del cliente.
/// </summary>
public sealed record RecordDeviceMetricsCommand(
    Guid PatientId,
    Guid EnrollmentId,
    Guid ActorId,
    decimal? Steps,
    decimal? DistanceM,
    decimal? ActivityKcal,
    decimal? SleepMinutes,
    DateTime? RecordedAt
) : IRequest<RecordedDeviceMetricsDto>;

public sealed class RecordDeviceMetricsCommandValidator
    : AbstractValidator<RecordDeviceMetricsCommand>
{
    public RecordDeviceMetricsCommandValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.EnrollmentId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Steps)
            .InclusiveBetween(1m, DeviceMetricLimits.MaxSteps)
            .When(x => x.Steps.HasValue)
            .WithMessage("Los pasos están fuera de rango.");
        RuleFor(x => x.DistanceM)
            .InclusiveBetween(1m, DeviceMetricLimits.MaxDistanceM)
            .When(x => x.DistanceM.HasValue)
            .WithMessage("La distancia está fuera de rango.");
        RuleFor(x => x.ActivityKcal)
            .InclusiveBetween(1m, DeviceMetricLimits.MaxActivityKcal)
            .When(x => x.ActivityKcal.HasValue)
            .WithMessage("Las calorías están fuera de rango.");
        RuleFor(x => x.SleepMinutes)
            .InclusiveBetween(1m, DeviceMetricLimits.MaxSleepMinutes)
            .When(x => x.SleepMinutes.HasValue)
            .WithMessage("El sueño está fuera de rango.");
        RuleFor(x => x)
            .Must(x =>
                x.Steps.HasValue
                || x.DistanceM.HasValue
                || x.ActivityKcal.HasValue
                || x.SleepMinutes.HasValue
            )
            .WithMessage("SIN_METRICAS: se requiere al menos una métrica del dispositivo.");
    }
}

/// <summary>
/// Persiste las métricas diarias del anillo (SPEC device-metrics-tracking) como
/// filas de <c>app.clinical_measurements</c>, UNA por día y métrica
/// (<c>UpsertDailyDeviceMetricsAsync</c>): los contadores acumulados del día
/// (pasos, distancia, kcal) se actualizan en el sitio, y el sueño de la última
/// sesión queda como el valor del día. Tras el commit se invalida la cache de
/// metrics-history para que la siguiente lectura incluya el dato nuevo.
/// </summary>
public sealed class RecordDeviceMetricsCommandHandler(
    IProgramRepository program,
    IClinicalMeasurementRepository measurements,
    ICacheService cache
) : IRequestHandler<RecordDeviceMetricsCommand, RecordedDeviceMetricsDto>
{
    private const string Source = "device";

    public async Task<RecordedDeviceMetricsDto> Handle(
        RecordDeviceMetricsCommand request,
        CancellationToken ct
    )
    {
        var enrollment = await program.GetEnrollmentAsync(request.EnrollmentId, ct);
        if (
            enrollment is null
            || enrollment.PatientId != request.PatientId
            || enrollment.Status != ProgramEnrollmentStatus.Active
        )
        {
            throw new NotFoundException(
                "NO_ACTIVE_ENROLLMENT: se requiere una inscripción activa del paciente."
            );
        }

        var zone = ResolveZone(enrollment.Timezone);
        var observed = request.RecordedAt switch
        {
            null => DateTime.UtcNow,
            { Kind: DateTimeKind.Utc } utc => utc,
            { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
            var unspecified => DateTime.SpecifyKind(unspecified.Value, DateTimeKind.Utc),
        };
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(observed, zone));
        var (dayStartUtc, dayEndUtc) = LocalDayBoundsUtc(localDate, zone);

        // Un solo fetch del catálogo; fail-closed si una métrica provista no está
        // sembrada (mismo criterio que el payload de signos vitales).
        var catalog = await measurements.GetActiveMetricsWithUnitsAsync(ct);
        var wanted = new List<(string Code, decimal Value)>(4);
        if (request.Steps is { } steps)
            wanted.Add(("step_count", steps));
        if (request.DistanceM is { } distance)
            wanted.Add(("distance_m", distance));
        if (request.ActivityKcal is { } kcal)
            wanted.Add(("activity_kcal", kcal));
        if (request.SleepMinutes is { } sleep)
            wanted.Add(("sleep_minutes", sleep));

        var rows = new List<DailyDeviceMetric>(wanted.Count);
        foreach (var (code, value) in wanted)
        {
            var metric = catalog.SingleOrDefault(m =>
                m.Code == code && m.DefaultUnit is { IsActive: true }
            );
            if (metric is null)
            {
                throw new UnprocessableEntityException(
                    $"DEVICE_METRIC_CATALOG_UNAVAILABLE: no está disponible el catálogo {code}."
                );
            }
            rows.Add(new DailyDeviceMetric(code, metric.Id, metric.DefaultUnitId, value));
        }

        await measurements.UpsertDailyDeviceMetricsAsync(
            request.PatientId,
            rows,
            dayStartUtc,
            dayEndUtc,
            observed,
            request.ActorId,
            Source,
            ct
        );
        await cache.RemoveAsync(
            CacheKeys.MetricsHistory(request.PatientId),
            CancellationToken.None
        );

        return new RecordedDeviceMetricsDto(localDate, rows.Select(r => r.Code).ToList(), observed);
    }

    private static TimeZoneInfo ResolveZone(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
            return TimeZoneInfo.Utc;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// Ventana UTC del día local. Los cambios de horario pueden hacer que la
    /// medianoche local no exista o se repita: se resuelve con la hora válida
    /// más cercana y se cierra la ventana en la medianoche local siguiente.
    /// </summary>
    private static (DateTime Start, DateTime End) LocalDayBoundsUtc(
        DateOnly localDate,
        TimeZoneInfo zone
    )
    {
        var start = LocalMidnightUtc(localDate, zone);
        var end = LocalMidnightUtc(localDate.AddDays(1), zone);
        if (end <= start)
            end = start.AddDays(1);
        return (start, end);
    }

    private static DateTime LocalMidnightUtc(DateOnly date, TimeZoneInfo zone)
    {
        var midnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(midnight))
        {
            midnight = midnight.AddHours(1);
        }
        return TimeZoneInfo.ConvertTimeToUtc(midnight, zone);
    }
}
