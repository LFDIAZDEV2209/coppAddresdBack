using CoppAddresd.Application.Features.Sos;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Sos;

/// <summary>
/// Pruebas adversariales de la activación SOS (tasks 3.2): semántica completa
/// de Idempotency-Key, validación de coordenadas, contacto E.164, rate-limit
/// y anti-abuso. Los códigos HTTP son el contrato del spec (REQ-SOS-01/02/03).
/// </summary>
public sealed class SosActivateTests
{
    private readonly ISosAlertRepository _repository = Substitute.For<ISosAlertRepository>();
    private readonly ISosRateLimiter _rateLimiter = Substitute.For<ISosRateLimiter>();
    private readonly ISosDispatchQueue _queue = Substitute.For<ISosDispatchQueue>();

    private readonly ActivateSosAlertHandler _handler;

    public SosActivateTests()
    {
        _rateLimiter
            .CheckAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(SosRateLimitDecision.Allow());
        _handler = new ActivateSosAlertHandler(
            _repository,
            _rateLimiter,
            _queue,
            NullLogger<ActivateSosAlertHandler>.Instance
        );
    }

    private static PatientProfile ProfileWithContact(string? phone) =>
        new()
        {
            Id = Guid.NewGuid(),
            FirstName = "Sofía",
            LastName = "Vega",
            EmergencyContact = phone is null
                ? null
                : System.Text.Json.JsonSerializer.Serialize(
                    new
                    {
                        name = "Ana",
                        relationship = "Familiar",
                        phone,
                        email = "ana@test.local",
                    }
                ),
        };

    // ---------- Validación de entrada (400 vía FluentValidation) ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-un-uuid")]
    [InlineData("11111111-1111-1111-1111-111111111111")] // v1, no v4
    [InlineData("6ba7b810-9dad-11d1-80b4-00c04fd430c8")] // v1 real
    public void Validator_ClaveAusenteOInvalida_Falla(string? idempotencyKey)
    {
        var validator = new ActivateSosAlertValidator();
        var command = new ActivateSosAlertCommand(Guid.NewGuid(), idempotencyKey ?? "");

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "IdempotencyKey");
    }

    [Fact]
    public void Validator_CoordenadasFueraDeRango_Falla()
    {
        var validator = new ActivateSosAlertValidator();

        var latInvalida = validator.Validate(
            new ActivateSosAlertCommand(Guid.NewGuid(), NewKeyV4(), Latitude: 120.5)
        );
        var lngInvalida = validator.Validate(
            new ActivateSosAlertCommand(Guid.NewGuid(), NewKeyV4(), Longitude: -210.0)
        );

        Assert.False(latInvalida.IsValid);
        Assert.False(lngInvalida.IsValid);
    }

    [Theory]
    [InlineData("3053924819", "+573053924819")] // local → país por defecto (+57)
    [InlineData("+57 305 392 4819", "+573053924819")] // formato libre
    [InlineData("+13055550123", "+13055550123")] // ya E.164
    [InlineData("573053924819", "+573053924819")] // con código país sin +
    [InlineData("abc", null)] // sin dígitos
    [InlineData("1234567", null)] // demasiado corto (7 dígitos)
    [InlineData("1234567890123456", null)] // demasiado largo (16 dígitos)
    public void NormalizePhoneE164_CasosRepresentativos(string raw, string? expected)
    {
        var actual = SosSupport.NormalizePhoneE164(raw);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("6ba7b810-9dad-11d1-80b4-00c04fd430c8", false)] // v1
    [InlineData("00000000-0000-0000-0000-000000000000", false)] // nil
    [InlineData("11111111-1111-2111-8111-111111111111", false)] // versión 2
    public void IsUuidV4_FormatosInvalidos(string value, bool expected)
    {
        Assert.Equal(expected, SosSupport.IsUuidV4(value));
    }

    [Fact]
    public void IsUuidV4_UnUuidV4Valido_Pasa()
    {
        Assert.True(SosSupport.IsUuidV4(Guid.NewGuid().ToString()));
    }

    [Fact]
    public void ComputePayloadHash_MismoPayload_MismoHash_Y_DistintoPayload_DistintoHash()
    {
        var now = DateTime.UtcNow;

        var h1 = SosSupport.ComputePayloadHash(4.7110, -74.0721, 12.5, now);
        var h2 = SosSupport.ComputePayloadHash(4.7110, -74.0721, 12.5, now);
        var h3 = SosSupport.ComputePayloadHash(4.7111, -74.0721, 12.5, now);
        var hSinCoords = SosSupport.ComputePayloadHash(null, null, null, null);

        Assert.Equal(h1, h2);
        Assert.NotEqual(h1, h3);
        Assert.NotEqual(h1, hSinCoords);
        Assert.Equal(64, h1.Length); // sha256 hex
    }

    // ---------- Handler: idempotencia, rate-limit, contacto ----------

    [Fact]
    public async Task Handle_ReplayMismoPayload_DevuelveAlertaOriginalSinReenviar()
    {
        var patientId = Guid.NewGuid();
        var key = NewKeyV4();
        var existing = new SosAlert
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            IdempotencyKey = key,
            PayloadHash = SosSupport.ComputePayloadHash(null, null, null, null),
            DestinationPhoneE164 = "+573053924819",
        };
        _repository
            .GetByPatientAndKeyAsync(patientId, key, Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await _handler.Handle(
            new ActivateSosAlertCommand(patientId, key),
            CancellationToken.None
        );

        Assert.Equal(SosActivationOutcome.Replayed, result.Outcome);
        Assert.Equal(existing.Id, result.Alert!.Id);

        // (REQ-SOS-01) El replay NO consulta contacto, NO consume cuota y NO
        // encola despacho (las notificaciones originales no se reenvían).
        await _repository
            .DidNotReceive()
            .GetPatientProfileAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _rateLimiter
            .DidNotReceive()
            .RegisterAttemptAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            );
        await _queue.DidNotReceive().EnqueueAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MismaClaveConPayloadDistinto_Conflicto409()
    {
        var patientId = Guid.NewGuid();
        var key = NewKeyV4();
        var existing = new SosAlert
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            IdempotencyKey = key,
            PayloadHash = SosSupport.ComputePayloadHash(4.7, -74.0, null, null),
            DestinationPhoneE164 = "+573053924819",
        };
        _repository
            .GetByPatientAndKeyAsync(patientId, key, Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await _handler.Handle(
            // Coordenadas distintas → hash distinto con la misma clave.
            new ActivateSosAlertCommand(patientId, key, Latitude: 5.1, Longitude: -75.0),
            CancellationToken.None
        );

        Assert.Equal(SosActivationOutcome.IdempotencyConflict, result.Outcome);
        await _queue.DidNotReceive().EnqueueAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlertaActivaExistenteConOtraClave_Conflicto409ConReferencia()
    {
        var patientId = Guid.NewGuid();
        var active = new SosAlert
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            IdempotencyKey = NewKeyV4(),
            DestinationPhoneE164 = "+573053924819",
        };
        _repository
            .GetByPatientAndKeyAsync(patientId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((SosAlert?)null);
        _repository
            .GetActiveByPatientAsync(patientId, Arg.Any<CancellationToken>())
            .Returns(active);
        _repository
            .GetPatientProfileAsync(patientId, Arg.Any<CancellationToken>())
            .Returns(ProfileWithContact("+573053924819"));
        _repository
            .GetAssignedStaffUserIdsAsync(patientId, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _handler.Handle(
            new ActivateSosAlertCommand(patientId, NewKeyV4()),
            CancellationToken.None
        );

        Assert.Equal(SosActivationOutcome.ActiveExists, result.Outcome);
        Assert.Equal(active.Id, result.Alert!.Id);

        // No se creó ni se consumió cuota ni se despachó nada.
        await _repository
            .DidNotReceive()
            .AddWithOutboxAsync(
                Arg.Any<SosAlert>(),
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            );
        await _queue.DidNotReceive().EnqueueAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SinContactoDeEmergencia_Lanza422SinCrearAlerta()
    {
        var patientId = Guid.NewGuid();
        _repository
            .GetByPatientAndKeyAsync(patientId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((SosAlert?)null);
        _repository
            .GetPatientProfileAsync(patientId, Arg.Any<CancellationToken>())
            .Returns(ProfileWithContact(phone: null));

        var exception = await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            _handler.Handle(
                new ActivateSosAlertCommand(patientId, NewKeyV4()),
                CancellationToken.None
            )
        );

        // (REQ-SOS-06) El mensaje NO revela el teléfono ni datos del contacto.
        Assert.DoesNotContain("3053", exception.Message);
        await _repository
            .DidNotReceive()
            .AddWithOutboxAsync(
                Arg.Any<SosAlert>(),
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Handle_ContactoNoNormalizable_Lanza422()
    {
        var patientId = Guid.NewGuid();
        _repository
            .GetByPatientAndKeyAsync(patientId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((SosAlert?)null);
        _repository
            .GetPatientProfileAsync(patientId, Arg.Any<CancellationToken>())
            .Returns(ProfileWithContact("sin-numero"));

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            _handler.Handle(
                new ActivateSosAlertCommand(patientId, NewKeyV4()),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task Handle_RateLimitDistribuido_Devuelve429AntesDeCrearODespachar()
    {
        var patientId = Guid.NewGuid();
        _rateLimiter
            .CheckAsync(
                patientId,
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(SosRateLimitDecision.Deny(40, "cooldown"));
        _repository
            .GetByPatientAndKeyAsync(patientId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((SosAlert?)null);
        _repository
            .GetPatientProfileAsync(patientId, Arg.Any<CancellationToken>())
            .Returns(ProfileWithContact("+573053924819"));

        var result = await _handler.Handle(
            new ActivateSosAlertCommand(patientId, NewKeyV4()),
            CancellationToken.None
        );

        Assert.Equal(SosActivationOutcome.RateLimited, result.Outcome);
        Assert.Equal(40, result.RetryAfterSeconds); // para el encabezado Retry-After

        // (REQ-SOS-02) No se persiste ni se tocan canales.
        await _repository
            .DidNotReceive()
            .AddWithOutboxAsync(
                Arg.Any<SosAlert>(),
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            );
        await _queue.DidNotReceive().EnqueueAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ActivacionExitosa_CreaConOutboxConsumeCuotaYEncola()
    {
        var patientId = Guid.NewGuid();
        var key = NewKeyV4();
        SosAlert? persisted = null;
        IReadOnlyCollection<string>? outboxKeys = null;

        _repository
            .GetByPatientAndKeyAsync(patientId, key, Arg.Any<CancellationToken>())
            .Returns((SosAlert?)null);
        _repository
            .GetPatientProfileAsync(patientId, Arg.Any<CancellationToken>())
            .Returns(ProfileWithContact("3053924819"));
        _repository
            .GetActiveByPatientAsync(patientId, Arg.Any<CancellationToken>())
            .Returns((SosAlert?)null);
        _repository
            .GetAssignedStaffUserIdsAsync(patientId, Arg.Any<CancellationToken>())
            .Returns([Guid.NewGuid(), Guid.NewGuid()]);
        _repository
            .AddWithOutboxAsync(
                Arg.Do<SosAlert>(a => persisted = a),
                Arg.Do<IReadOnlyCollection<string>>(k => outboxKeys = k),
                Arg.Any<CancellationToken>()
            )
            .Returns(new SosCreateOutcome(SosCreateResult.Created, null));

        var result = await _handler.Handle(
            new ActivateSosAlertCommand(patientId, key, Latitude: 4.7110, Longitude: -74.0721),
            CancellationToken.None
        );

        // 201 Created (el controller mapea el outcome).
        Assert.Equal(SosActivationOutcome.Created, result.Outcome);
        Assert.NotNull(persisted);
        Assert.Equal(SosAlertStatus.Activa, persisted!.Status);
        Assert.Equal("+573053924819", persisted.DestinationPhoneE164); // normalizado server-side
        Assert.Equal(4.7110, persisted.Latitude);

        // Outbox: 1 fila SMS + 2 push (uno por profesional asignado).
        Assert.NotNull(outboxKeys);
        Assert.Equal(3, outboxKeys!.Count);
        Assert.Contains($"sos:sms:{persisted.Id}", outboxKeys);
        Assert.Contains(
            outboxKeys,
            k => k.StartsWith($"sos:push:{persisted.Id}:", StringComparison.Ordinal)
        );

        await _rateLimiter
            .Received(1)
            .RegisterAttemptAsync(patientId, "+573053924819", null, Arg.Any<CancellationToken>());
        await _queue.Received(1).EnqueueAsync(persisted.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CarreraDeIdempotenciaMismoPayload_SeResuelveComoReplay()
    {
        var patientId = Guid.NewGuid();
        var key = NewKeyV4();
        var hash = SosSupport.ComputePayloadHash(null, null, null, null);
        var winner = new SosAlert
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            IdempotencyKey = key,
            PayloadHash = hash,
            DestinationPhoneE164 = "+573053924819",
        };

        _repository
            .GetByPatientAndKeyAsync(patientId, key, Arg.Any<CancellationToken>())
            .Returns((SosAlert?)null, winner); // fast path miss; tras colisión, la fila ganadora
        _repository
            .GetPatientProfileAsync(patientId, Arg.Any<CancellationToken>())
            .Returns(ProfileWithContact("+573053924819"));
        _repository
            .GetActiveByPatientAsync(patientId, Arg.Any<CancellationToken>())
            .Returns((SosAlert?)null);
        _repository
            .GetAssignedStaffUserIdsAsync(patientId, Arg.Any<CancellationToken>())
            .Returns([]);
        _repository
            .AddWithOutboxAsync(
                Arg.Any<SosAlert>(),
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new SosCreateOutcome(SosCreateResult.IdempotencyCollision, winner));

        var result = await _handler.Handle(
            new ActivateSosAlertCommand(patientId, key),
            CancellationToken.None
        );

        // Misma clave + payload idéntico bajo carrera → 200 con la original.
        Assert.Equal(SosActivationOutcome.Replayed, result.Outcome);
        Assert.Equal(winner.Id, result.Alert!.Id);
        await _queue.DidNotReceive().EnqueueAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CarreraDeAlertaActiva_Devuelve409ConLaExistente()
    {
        var patientId = Guid.NewGuid();
        var winner = new SosAlert
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            IdempotencyKey = NewKeyV4(),
            DestinationPhoneE164 = "+573053924819",
        };

        _repository
            .GetByPatientAndKeyAsync(patientId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((SosAlert?)null);
        _repository
            .GetPatientProfileAsync(patientId, Arg.Any<CancellationToken>())
            .Returns(ProfileWithContact("+573053924819"));
        _repository
            .GetActiveByPatientAsync(patientId, Arg.Any<CancellationToken>())
            .Returns((SosAlert?)null);
        _repository
            .GetAssignedStaffUserIdsAsync(patientId, Arg.Any<CancellationToken>())
            .Returns([]);
        _repository
            .AddWithOutboxAsync(
                Arg.Any<SosAlert>(),
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new SosCreateOutcome(SosCreateResult.ActiveCollision, winner));

        var result = await _handler.Handle(
            new ActivateSosAlertCommand(patientId, NewKeyV4()),
            CancellationToken.None
        );

        // El índice parcial de PostgreSQL decidió: 409 con la alerta activa.
        Assert.Equal(SosActivationOutcome.ActiveExists, result.Outcome);
        Assert.Equal(winner.Id, result.Alert!.Id);
        await _queue.DidNotReceive().EnqueueAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private static string NewKeyV4() => Guid.NewGuid().ToString();
}
