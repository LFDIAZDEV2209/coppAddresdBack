using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Sos;

/// <summary>
/// Pruebas de los despachadores de canales SOS (tasks 2.3/2.4, REQ-SOS-03/04
/// y REQ-SOS-06): outbox durable, degradación sin 500, purga de tokens solo
/// ante Unregistered inequívoco y PRIVACIDAD DE LOGS (prohibido registrar
/// coordenadas, teléfonos o el cuerpo del SMS).
/// </summary>
public sealed class SosDispatchersTests
{
    private const string PhoneE164 = "+573053924819";
    private const double Latitude = 4.7110;
    private const double Longitude = -74.0721;

    private readonly INotificationDedupeRepository _dedupe =
        Substitute.For<INotificationDedupeRepository>();
    private readonly ISosAlertRepository _alerts = Substitute.For<ISosAlertRepository>();

    private static SosAlert NewAlert() =>
        new()
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            IdempotencyKey = Guid.NewGuid().ToString(),
            PayloadHash = "hash",
            DestinationPhoneE164 = PhoneE164,
            Latitude = Latitude,
            Longitude = Longitude,
            Patient = new PatientProfile { FirstName = "Sofía", LastName = "Vega" },
        };

    // ===================== SMS =====================

    [Fact]
    public async Task Sms_Exitoso_MarcaEnviadoYDedupePersistente()
    {
        var sms = Substitute.For<ISmsSender>();
        sms.IsConfigured.Returns(true);
        sms.SendAsync(PhoneE164, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmsSendResult(true, "SM123", null));
        _dedupe
            .GetByKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((NotificationDedupeKey?)null);

        var alert = NewAlert();
        var dispatcher = new SosSmsDispatcher(
            sms,
            _dedupe,
            CreateDbContext(),
            NullLogger<SosSmsDispatcher>.Instance
        );

        var status = await dispatcher.DispatchAsync(alert, CancellationToken.None);

        // (REQ-SOS-03) Canal marcado Enviado con la alerta Activa (201 ya dado).
        Assert.Equal(SosChannelStatus.Enviado, status);
        await sms.Received(1).SendAsync(PhoneE164, Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Outbox: el estado final queda durable (sent = pegajoso).
        await _dedupe
            .Received(1)
            .UpsertAsync(
                $"sos:sms:{alert.Id}",
                Arg.Any<Guid>(),
                Arg.Any<string?>(),
                "enviado",
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Sms_YaEnviadoNoSeReenviaAunqueElProcesadorReintente()
    {
        var sms = Substitute.For<ISmsSender>();
        sms.IsConfigured.Returns(true);
        _dedupe
            .GetByKeyAsync($"sos:sms:{NewAlert().Id}", Arg.Any<CancellationToken>())
            .Returns((NotificationDedupeKey?)null);

        var alert = NewAlert();
        _dedupe
            .GetByKeyAsync($"sos:sms:{alert.Id}", Arg.Any<CancellationToken>())
            .Returns(
                new NotificationDedupeKey
                {
                    DedupeKey = $"sos:sms:{alert.Id}",
                    SmsStatus = "enviado",
                }
            );

        var dispatcher = new SosSmsDispatcher(
            sms,
            _dedupe,
            CreateDbContext(),
            NullLogger<SosSmsDispatcher>.Instance
        );

        await dispatcher.DispatchAsync(alert, CancellationToken.None);

        // (D2) Los reintentos jamás reenvían un canal ya entregado.
        await sms.DidNotReceive()
            .SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sms_SinCredenciales_DegradaANoConfiguradoSinEnviar()
    {
        var sms = Substitute.For<ISmsSender>();
        sms.IsConfigured.Returns(false);
        _dedupe
            .GetByKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((NotificationDedupeKey?)null);

        var dispatcher = new SosSmsDispatcher(
            sms,
            _dedupe,
            CreateDbContext(),
            NullLogger<SosSmsDispatcher>.Instance
        );

        var status = await dispatcher.DispatchAsync(NewAlert(), CancellationToken.None);

        // (REQ-SOS-03) Sin credenciales → canal registrado NoConfigurado, la
        // alerta permanece Activa y la respuesta al paciente ya fue 201.
        Assert.Equal(SosChannelStatus.NoConfigurado, status);
        await sms.DidNotReceive()
            .SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _dedupe
            .Received(1)
            .UpsertAsync(
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<string?>(),
                "noconfigurado",
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Sms_TimeoutReiterado_DegradadoSinLanzar500()
    {
        var sms = Substitute.For<ISmsSender>();
        sms.IsConfigured.Returns(true);
        // El proveedor se cuelga: el CancellationToken del intento cancela y
        // el dispatcher aplica los 2 reintentos antes de degradar (3 llamadas).
        sms.SendAsync(PhoneE164, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
                Task.FromException<SmsSendResult>(
                    new OperationCanceledException((CancellationToken)callInfo[2])
                )
            );
        _dedupe
            .GetByKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((NotificationDedupeKey?)null);

        var dispatcher = new SosSmsDispatcher(
            sms,
            _dedupe,
            CreateDbContext(),
            NullLogger<SosSmsDispatcher>.Instance
        );

        var status = await dispatcher.DispatchAsync(NewAlert(), CancellationToken.None);

        // (REQ-SOS-03) Timeout con hasta 2 reintentos → canal Timeout,
        // sin excepción (la alerta permanece Activa y el 201 ya fue entregado).
        Assert.Equal(SosChannelStatus.Timeout, status);
        await sms.Received(3).SendAsync(PhoneE164, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ===================== Push FCM =====================

    [Fact]
    public async Task Push_TokenInvalidPurgaEseTokenYNoAborta()
    {
        var fcm = Substitute.For<IFcmClient>();
        var tokens = Substitute.For<IDeviceTokenRepository>();
        var staffUserId = Guid.NewGuid();
        var token = "fcm-token-obsoleto";

        _alerts
            .GetAssignedStaffUserIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([staffUserId]);
        tokens
            .GetByUserIdAsync(staffUserId, Arg.Any<CancellationToken>())
            .Returns([
                new DeviceToken
                {
                    Id = Guid.NewGuid(),
                    UserId = staffUserId,
                    Token = token,
                    Platform = "android",
                },
            ]);
        fcm.SendAsync(
                token,
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new FcmSendResult(FcmSendStatus.TokenInvalid, "UNREGISTERED", "404"));
        _dedupe
            .GetByKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((NotificationDedupeKey?)null);

        var dispatcher = new SosPushDispatcher(
            _alerts,
            tokens,
            fcm,
            _dedupe,
            NullLogger<SosPushDispatcher>.Instance
        );

        var result = await dispatcher.DispatchAsync(NewAlert(), CancellationToken.None);

        // (D4) Purga ÚNICAMENTE ante Unregistered inequívoco: el token se
        // elimina y el flujo no aborta (0 alcanzados por ese token, canal Fallido).
        await tokens.Received(1).DeleteByTokenAsync(token, Arg.Any<CancellationToken>());
        Assert.Equal(SosChannelStatus.Fallido, result.Status);
        Assert.Equal(0, result.Recipients);
    }

    [Fact]
    public async Task Push_ErrorTransitorio503_PreservaElToken()
    {
        var fcm = Substitute.For<IFcmClient>();
        var tokens = Substitute.For<IDeviceTokenRepository>();
        var staffUserId = Guid.NewGuid();
        var token = "fcm-token-valido";

        _alerts
            .GetAssignedStaffUserIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([staffUserId]);
        tokens
            .GetByUserIdAsync(staffUserId, Arg.Any<CancellationToken>())
            .Returns([
                new DeviceToken
                {
                    Id = Guid.NewGuid(),
                    UserId = staffUserId,
                    Token = token,
                    Platform = "android",
                },
            ]);
        fcm.SendAsync(
                token,
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new FcmSendResult(FcmSendStatus.Error, "503", "Service Unavailable"));
        _dedupe
            .GetByKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((NotificationDedupeKey?)null);

        var dispatcher = new SosPushDispatcher(
            _alerts,
            tokens,
            fcm,
            _dedupe,
            NullLogger<SosPushDispatcher>.Instance
        );

        await dispatcher.DispatchAsync(NewAlert(), CancellationToken.None);

        // (D4) Ante fallo transitorio el token SE PRESERVA para futuros envíos.
        await tokens
            .DidNotReceive()
            .DeleteByTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Push_SegundoDespachoNoDuplicaEnvios()
    {
        var fcm = Substitute.For<IFcmClient>();
        var tokens = Substitute.For<IDeviceTokenRepository>();
        var staffUserId = Guid.NewGuid();
        var token = "fcm-token";

        var alert = NewAlert();
        _alerts
            .GetAssignedStaffUserIdsAsync(alert.PatientId, Arg.Any<CancellationToken>())
            .Returns([staffUserId]);
        tokens
            .GetByUserIdAsync(staffUserId, Arg.Any<CancellationToken>())
            .Returns([
                new DeviceToken
                {
                    Id = Guid.NewGuid(),
                    UserId = staffUserId,
                    Token = token,
                    Platform = "android",
                },
            ]);
        fcm.SendAsync(
                token,
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new FcmSendResult(FcmSendStatus.Sent));
        _dedupe
            .GetByKeyAsync($"sos:push:{alert.Id}:{staffUserId}", Arg.Any<CancellationToken>())
            .Returns(
                new NotificationDedupeKey
                {
                    DedupeKey = $"sos:push:{alert.Id}:{staffUserId}",
                    PushStatus = "enviado",
                }
            );

        var dispatcher = new SosPushDispatcher(
            _alerts,
            tokens,
            fcm,
            _dedupe,
            NullLogger<SosPushDispatcher>.Instance
        );

        await dispatcher.DispatchAsync(alert, CancellationToken.None);

        // Outbox durable: el destinatario ya recibió el push → 0 envíos extra.
        await fcm.DidNotReceive()
            .SendAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>()
            );
    }

    // ===================== Privacidad de logs (REQ-SOS-06) =====================

    [Fact]
    public async Task Logs_SinPii_NiCoordenadasNiTelefonoNiCuerpoSms()
    {
        var sms = Substitute.For<ISmsSender>();
        sms.IsConfigured.Returns(true);
        sms.SendAsync(PhoneE164, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmsSendResult(true, "SM1", null));
        _dedupe
            .GetByKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((NotificationDedupeKey?)null);

        var collector = new SosLogCollector();
        var alert = NewAlert();
        var smsBody = SosSmsTemplate.Build(alert.Patient!.FirstName, alert.Id);

        var smsDispatcher = new SosSmsDispatcher(
            sms,
            _dedupe,
            CreateDbContext(),
            collector.For<SosSmsDispatcher>()
        );

        await smsDispatcher.DispatchAsync(alert, CancellationToken.None);

        // (REQ-SOS-06) Ninguna línea de log contiene el teléfono completo, las
        // coordenadas GPS ni el cuerpo del SMS: solo alertId/canal/estado.
        foreach (var line in collector.Lines)
        {
            Assert.False(line.Contains(PhoneE164, StringComparison.Ordinal), $"PII en log: {line}");
            Assert.False(
                line.Contains("3053924819", StringComparison.Ordinal),
                $"PII en log: {line}"
            );
            Assert.False(
                line.Contains(Latitude.ToString(), StringComparison.Ordinal)
                    || line.Contains(Longitude.ToString(), StringComparison.Ordinal),
                $"Coordenadas en log: {line}"
            );
            Assert.False(
                line.Contains(smsBody, StringComparison.Ordinal),
                $"Cuerpo de SMS en log: {line}"
            );
        }

        // Además, el flujo SIEMPRE registró trazas correlacionadas (alertId).
        Assert.Contains(
            collector.Lines,
            l => l.Contains(alert.Id.ToString(), StringComparison.Ordinal)
        );
    }

    private static AppDbContext CreateDbContext()
    {
        // DbContext NO usado por los tests de dispatcher (los estados de canal
        // de alerta se persisterían contra BD real): se pasa uno InMemory
        // para satisfacer el constructor sin tocar BD.
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"sos-dispatch-{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }
}

/// <summary>ILogger que captura las líneas emitidas para verificar el contrato de privacidad.</summary>
public sealed class SosLogCollector : ILoggerFactory
{
    public List<string> Lines { get; } = [];

    public ILogger CreateLogger(string categoryName) => new CollectorLogger(this);

    public ILogger<T> For<T>() => new ForwardingLogger<T>(this);

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }

    private sealed class CollectorLogger(SosLogCollector owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            owner.Lines.Add(formatter(state, exception));
        }
    }

    private sealed class ForwardingLogger<T>(SosLogCollector owner) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            owner.Lines.Add(formatter(state, exception));
        }
    }
}
