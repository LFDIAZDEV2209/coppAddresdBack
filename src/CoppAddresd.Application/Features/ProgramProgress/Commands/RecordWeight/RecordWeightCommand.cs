using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.RecordWeight;

// Límites de entrada ya usados en CreatePatient y CompleteTask; no son metas clínicas.
public static class WeightInputLimits
{
    public const decimal Min = 1m;
    public const decimal Max = 500m;
}

public sealed record RecordWeightRequest(decimal WeightKg, DateOnly Date);
public sealed record RecordedWeightDto(Guid Id, decimal WeightKg, DateOnly Date, DateTime ObservedAt);
// Identidad resuelta por la API, nunca vinculada al body del cliente.
public sealed record RecordWeightCommand(Guid PatientId, Guid EnrollmentId, Guid ActorId,
    decimal WeightKg, DateOnly Date) : IRequest<RecordedWeightDto>;

public sealed class RecordWeightCommandValidator : AbstractValidator<RecordWeightCommand>
{
    public RecordWeightCommandValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.EnrollmentId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.WeightKg).InclusiveBetween(WeightInputLimits.Min, WeightInputLimits.Max)
            .WithMessage("El peso debe estar entre 1 y 500 kg.");
        RuleFor(x => x.WeightKg).Must(value => decimal.Round(value, 2) == value)
            .WithMessage("El peso admite como máximo dos decimales.");
        RuleFor(x => x.Date).NotEmpty().WithMessage("La fecha es requerida.");
    }
}

public sealed class RecordWeightCommandHandler(
    IProgramRepository program,
    IClinicalMeasurementRepository measurements,
    ICacheService cache) : IRequestHandler<RecordWeightCommand, RecordedWeightDto>
{
    public async Task<RecordedWeightDto> Handle(RecordWeightCommand request, CancellationToken ct)
    {
        var enrollment = await program.GetEnrollmentAsync(request.EnrollmentId, ct);
        if (enrollment is null || enrollment.PatientId != request.PatientId || enrollment.Status != ProgramEnrollmentStatus.Active)
            throw new NotFoundException("NO_ACTIVE_ENROLLMENT: se requiere una inscripción activa del paciente.");

        var now = DateTime.UtcNow;
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(enrollment.Timezone); }
        catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { zone = TimeZoneInfo.Utc; }
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, zone);
        if (request.Date > DateOnly.FromDateTime(localNow))
            throw new UnprocessableEntityException("FUTURE_MEASUREMENT_DATE: no se permite una fecha futura.");

        // La fecha es la elegida por el paciente en su zona; hora de la captura actual.
        var localObserved = request.Date.ToDateTime(TimeOnly.FromDateTime(localNow), DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(localObserved))
            throw new UnprocessableEntityException("INVALID_MEASUREMENT_DATE: la hora no existe en la zona del paciente.");
        var observed = request.Date == DateOnly.FromDateTime(localNow) ? now : TimeZoneInfo.ConvertTimeToUtc(localObserved, zone);
        var metric = (await measurements.GetActiveMetricsWithUnitsAsync(ct))
            .SingleOrDefault(m => m.Code == "weight" && m.DefaultUnit is { IsActive: true, Code: "kg" });
        if (metric is null)
            throw new UnprocessableEntityException("WEIGHT_CATALOG_UNAVAILABLE: no está disponible el catálogo weight/kg.");

        var measurement = new ClinicalMeasurement
        {
            Id = Guid.NewGuid(), PatientId = request.PatientId, MetricId = metric.Id,
            UnitId = metric.DefaultUnitId, Value = request.WeightKg, ObservedAt = observed,
            RecordedAt = now, CreatedAt = now, CreatedBy = request.ActorId, Source = "patient"
        };
        await measurements.AddBatchAsync([measurement], ct);
        // Tras el commit, la siguiente lectura debe reconstruirse con la nueva medición.
        await cache.RemoveAsync(CacheKeys.MetricsHistory(request.PatientId), CancellationToken.None);
        return new(measurement.Id, measurement.Value, request.Date, measurement.ObservedAt);
    }
}
