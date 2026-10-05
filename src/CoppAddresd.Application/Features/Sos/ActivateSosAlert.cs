using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Sos;

// ===================== Activación de alerta SOS =====================

/// <summary>
/// Activación del botón de pánico (REQ-SOS-01/02/03). El PatientId SIEMPRE
/// viene derivado del JWT (el controller, nunca el body). La persistencia es
/// atómica (índice único de idempotencia + parcial de alerta activa) y el
/// despacho de canales se encola tras el commit (outbox durable en
/// <c>app.notification_dedupe_keys</c>).
/// </summary>
public record ActivateSosAlertCommand(
    Guid PatientId,
    string IdempotencyKey,
    double? Latitude = null,
    double? Longitude = null,
    double? AccuracyMeters = null,
    DateTime? LocationCapturedAt = null,
    SosVitalsDto? Vitals = null,
    string? DeviceId = null
) : IRequest<ActivateSosAlertResult>;

public sealed class ActivateSosAlertValidator : AbstractValidator<ActivateSosAlertCommand>
{
    public ActivateSosAlertValidator()
    {
        // Idempotency-Key obligatoria y estrictamente UUIDv4 (400 si falta o
        // es inválida — REQ-SOS-01). Se normaliza a minúsculas en el handler.
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("El encabezado Idempotency-Key es requerido.")
            .Must(SosSupport.IsUuidV4)
            .WithMessage("El encabezado Idempotency-Key debe ser un UUID v4 válido.");

        // Coordenadas fuera de rango → 400 (REQ-SOS-01). Se descartan en el
        // handler solo cuando el cliente las envió nulas.
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90.0, 90.0)
            .When(x => x.Latitude.HasValue)
            .WithMessage("La latitud está fuera de rango [-90, 90].");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180.0, 180.0)
            .When(x => x.Longitude.HasValue)
            .WithMessage("La longitud está fuera de rango [-180, 180].");

        RuleFor(x => x.AccuracyMeters)
            .InclusiveBetween(0, 100_000)
            .When(x => x.AccuracyMeters.HasValue)
            .WithMessage("La precisión de la ubicación es inválida.");

        // Signos vitales opcionales (demo): rangos fisiológicos razonables.
        RuleFor(x => x.Vitals!.HeartRate)
            .InclusiveBetween(20, 300)
            .When(x => x.Vitals?.HeartRate.HasValue == true)
            .WithMessage("La frecuencia cardíaca es inválida.");

        RuleFor(x => x.Vitals!.Spo2)
            .InclusiveBetween(50, 100)
            .When(x => x.Vitals?.Spo2.HasValue == true)
            .WithMessage("La saturación de oxígeno es inválida.");

        RuleFor(x => x.Vitals!.BloodPressure)
            .MaximumLength(20)
            .When(x => !string.IsNullOrWhiteSpace(x.Vitals?.BloodPressure))
            .WithMessage("La presión arterial es inválida.");

        RuleFor(x => x.DeviceId)
            .MaximumLength(64)
            .When(x => x.DeviceId is not null)
            .WithMessage("El identificador de dispositivo excede el máximo permitido.");
    }
}

public sealed class ActivateSosAlertHandler(
    ISosAlertRepository repository,
    ISosRateLimiter rateLimiter,
    ISosDispatchQueue dispatchQueue,
    ILogger<ActivateSosAlertHandler> logger
) : IRequestHandler<ActivateSosAlertCommand, ActivateSosAlertResult>
{
    public async Task<ActivateSosAlertResult> Handle(
        ActivateSosAlertCommand request,
        CancellationToken ct
    )
    {
        var idempotencyKey = request.IdempotencyKey.Trim().ToLowerInvariant();
        var payloadHash = SosSupport.ComputePayloadHash(
            request.Latitude,
            request.Longitude,
            request.AccuracyMeters,
            request.LocationCapturedAt
        );

        // --- 1) Fast path de idempotencia (replay o colisión de payload) ---
        var existing = await repository.GetByPatientAndKeyAsync(
            request.PatientId,
            idempotencyKey,
            ct
        );
        if (existing is not null)
        {
            return existing.PayloadHash == payloadHash
                ? new ActivateSosAlertResult(
                    SosActivationOutcome.Replayed,
                    SosAlertDto.FromEntity(existing),
                    0
                )
                : new ActivateSosAlertResult(SosActivationOutcome.IdempotencyConflict, null, 0);
        }

        // --- 2) Contacto de emergencia con teléfono E.164 (422 antes de crear) ---
        var profile = await repository.GetPatientProfileAsync(request.PatientId, ct);
        if (profile is null)
        {
            throw new UnprocessableEntityException(
                "Configura un contacto de emergencia con teléfono válido antes de activar el SOS."
            );
        }

        var e164 = SosSupport.NormalizePhoneE164(
            SosSupport.ExtractEmergencyContactPhone(profile.EmergencyContact)
        );
        if (e164 is null)
        {
            throw new UnprocessableEntityException(
                "Configura un contacto de emergencia con teléfono válido antes de activar el SOS."
            );
        }

        // --- 3) Rate-limit distribuido ANTES de persistir o invocar canales ---
        var decision = await rateLimiter.CheckAsync(request.PatientId, e164, request.DeviceId, ct);
        if (!decision.Allowed)
        {
            logger.LogWarning(
                "SOS rate-limited: patientId={PatientId}, reason={Reason}, retryAfter={RetryAfter}s.",
                request.PatientId,
                decision.Reason,
                decision.RetryAfterSeconds
            );
            return new ActivateSosAlertResult(
                SosActivationOutcome.RateLimited,
                null,
                decision.RetryAfterSeconds
            );
        }

        // --- 4) Carrera: ¿ya hay una alerta activa con OTRA clave? ---
        var active = await repository.GetActiveByPatientAsync(request.PatientId, ct);
        if (active is not null)
        {
            return new ActivateSosAlertResult(
                SosActivationOutcome.ActiveExists,
                SosAlertDto.FromEntity(active),
                0
            );
        }

        // --- 5) Creación atómica (alerta + outbox de dedupe) en UNA transacción ---
        var alert = new SosAlert
        {
            Id = Guid.NewGuid(),
            PatientId = request.PatientId,
            IdempotencyKey = idempotencyKey,
            PayloadHash = payloadHash,
            Status = SosAlertStatus.Activa,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            AccuracyMeters = request.AccuracyMeters,
            // Normaliza a UTC explícito: timestamptz en PostgreSQL exige
            // DateTimeKind.Utc para evitar conversión de zona local.
            LocationCapturedAt = request.LocationCapturedAt is null
                ? null
                : DateTime.SpecifyKind(request.LocationCapturedAt.Value, DateTimeKind.Utc),
            HeartRate = request.Vitals?.HeartRate,
            Spo2 = request.Vitals?.Spo2,
            BloodPressure = request.Vitals?.BloodPressure,
            DestinationPhoneE164 = e164,
        };

        // Outbox: sms + una fila por profesional asignado (destinatarios del push).
        var staffUserIds = await repository.GetAssignedStaffUserIdsAsync(request.PatientId, ct);
        var dedupeKeys = BuildDedupeKeys(alert.Id, staffUserIds);

        var outcome = await repository.AddWithOutboxAsync(alert, dedupeKeys, ct);

        switch (outcome.Result)
        {
            case SosCreateResult.Created:
                break;

            case SosCreateResult.IdempotencyCollision:
            {
                // Otra petición creó la fila con esta clave entre el fast path
                // y el insert: misma lógica que el replay (hash → 200/409).
                var row = outcome.ConflictingAlert;
                if (row is not null && row.PayloadHash == payloadHash)
                {
                    return new ActivateSosAlertResult(
                        SosActivationOutcome.Replayed,
                        SosAlertDto.FromEntity(row),
                        0
                    );
                }

                return new ActivateSosAlertResult(
                    SosActivationOutcome.IdempotencyConflict,
                    null,
                    0
                );
            }

            default:
            {
                // Otra petición creó la alerta activa del paciente: 409 con su
                // referencia (REQ-SOS-01, scenario "alerta activa con distinta
                // clave" bajo carrera — el índice parcial decidió).
                var conflicting = outcome.ConflictingAlert;
                return new ActivateSosAlertResult(
                    SosActivationOutcome.ActiveExists,
                    conflicting is null ? null : SosAlertDto.FromEntity(conflicting),
                    0
                );
            }
        }

        // --- 6) Post-commit: consumo de cuota + encolado del despacho ---
        // (best-effort: si falla el consumo en caché, la cuota de esta alerta
        // no se descuenta — degradación aceptable y logueada; el hard-guarantee
        // de una sola alerta activa vive en el índice parcial de PostgreSQL).
        await rateLimiter.RegisterAttemptAsync(request.PatientId, e164, request.DeviceId, ct);

        // (REQ-SOS-06) Logs SIN PII: alertId/patientId/estados únicamente.
        logger.LogInformation(
            "SOS alerta creada: alertId={AlertId}, patientId={PatientId}, staffPushTargets={StaffTargets}.",
            alert.Id,
            request.PatientId,
            staffUserIds.Count
        );

        await dispatchQueue.EnqueueAsync(alert.Id, ct);

        return new ActivateSosAlertResult(
            SosActivationOutcome.Created,
            SosAlertDto.FromEntity(alert),
            0
        );
    }

    /// <summary>Claves de outbox: una por canal (sms, voz, correo) y una por destinatario push.</summary>
    public static IReadOnlyList<string> BuildDedupeKeys(
        Guid alertId,
        IReadOnlyList<Guid> staffUserIds
    )
    {
        var keys = new List<string>(3 + staffUserIds.Count)
        {
            $"sos:sms:{alertId}",
            $"sos:voice:{alertId}",
            $"sos:email:{alertId}",
        };
        keys.AddRange(staffUserIds.Distinct().Select(userId => $"sos:push:{alertId}:{userId}"));
        return keys;
    }
}
