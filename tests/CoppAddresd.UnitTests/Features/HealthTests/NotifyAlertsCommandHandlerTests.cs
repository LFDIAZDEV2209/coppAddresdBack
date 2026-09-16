using CoppAddresd.Application.Features.HealthTests.Notifications;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.HealthTests;

public sealed class NotifyAlertsCommandHandlerTests
{
    [Fact]
    public async Task Preview_no_envia_ni_registra_y_marca_queued()
    {
        var patient = BuildPatient();
        var alert = BuildAlert(patient.Id);
        var (handler, notifRepo, _, sms, _) = BuildHandler([alert], patient);

        var result = await handler.Handle(
            new NotifyAlertsCommand(
                new NotifyAlertsRequest(
                    [alert.Id],
                    [NotificationChannel.sms],
                    BodyOverride: "Hola [paciente]",
                    Preview: true
                )
            ),
            CancellationToken.None
        );

        Assert.True(result.Preview);
        Assert.Equal(1, result.Requested);
        var item = Assert.Single(result.Items);
        Assert.Equal(NotificationStatus.queued, item.Status);
        Assert.Equal("Hola Ana Pérez", item.RenderedBody);
        await sms.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
        await notifRepo.DidNotReceiveWithAnyArgs().AddNotificationsAsync(default!, default);
    }

    [Fact]
    public async Task Sms_marca_skipped_cuando_el_paciente_no_tiene_telefono()
    {
        var patient = BuildPatient(phone: null);
        var alert = BuildAlert(patient.Id);
        var (handler, _, _, sms, _) = BuildHandler([alert], patient);

        var result = await handler.Handle(
            new NotifyAlertsCommand(new NotifyAlertsRequest([alert.Id], [NotificationChannel.sms])),
            CancellationToken.None
        );

        var item = Assert.Single(result.Items);
        Assert.Equal(NotificationStatus.skipped, item.Status);
        Assert.Contains("teléfono", item.Reason);
        Assert.Equal(1, result.Skipped);
        await sms.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }

    [Fact]
    public async Task Community_marca_skipped_cuando_el_paciente_no_tiene_cuenta()
    {
        var patient = BuildPatient(hasCommunityAccount: false);
        var alert = BuildAlert(patient.Id);
        var (handler, _, _, _, community) = BuildHandler([alert], patient);

        var result = await handler.Handle(
            new NotifyAlertsCommand(new NotifyAlertsRequest([alert.Id], [NotificationChannel.community])),
            CancellationToken.None
        );

        var item = Assert.Single(result.Items);
        Assert.Equal(NotificationStatus.skipped, item.Status);
        Assert.Contains("app", item.Reason);
        await community.DidNotReceiveWithAnyArgs().SendDirectMessageAsync(default, default!, default, default);
    }

    [Fact]
    public async Task Sms_exitoso_marca_sent_y_registra_la_entrega()
    {
        var patient = BuildPatient();
        var alert = BuildAlert(patient.Id);
        var (handler, notifRepo, _, sms, _) = BuildHandler([alert], patient);
        sms.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmsSendResult(true, "msg-1", null));

        var result = await handler.Handle(
            new NotifyAlertsCommand(new NotifyAlertsRequest([alert.Id], [NotificationChannel.sms])),
            CancellationToken.None
        );

        Assert.Equal(1, result.Sent);
        await notifRepo.Received(1)
            .AddNotificationsAsync(
                Arg.Is<IReadOnlyList<HealthTestNotification>>(list =>
                    list.Count == 1
                    && list[0].Status == NotificationStatus.sent
                    && list[0].ProviderMessageId == "msg-1"
                    && list[0].AlertId == alert.Id
                    && list[0].SentAt != null
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Sms_fallido_marca_failed_y_guarda_el_error()
    {
        var patient = BuildPatient();
        var alert = BuildAlert(patient.Id);
        var (handler, notifRepo, _, sms, _) = BuildHandler([alert], patient);
        sms.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmsSendResult(false, null, "proveedor caído"));

        var result = await handler.Handle(
            new NotifyAlertsCommand(new NotifyAlertsRequest([alert.Id], [NotificationChannel.sms])),
            CancellationToken.None
        );

        Assert.Equal(1, result.Failed);
        await notifRepo.Received(1)
            .AddNotificationsAsync(
                Arg.Is<IReadOnlyList<HealthTestNotification>>(list =>
                    list.Count == 1
                    && list[0].Status == NotificationStatus.failed
                    && list[0].Error == "proveedor caído"
                    && list[0].SentAt == null
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task El_body_override_tiene_precedencia_sobre_la_plantilla()
    {
        var patient = BuildPatient();
        var alert = BuildAlert(patient.Id);
        var template = BuildTemplate(NotificationChannel.sms, "PLANTILLA [valor]");
        var (handler, _, _, sms, _) = BuildHandler([alert], patient, [template]);
        sms.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmsSendResult(true, "msg-2", null));

        var result = await handler.Handle(
            new NotifyAlertsCommand(
                new NotifyAlertsRequest([alert.Id], [NotificationChannel.sms], BodyOverride: "OVERRIDE [paciente]")
            ),
            CancellationToken.None
        );

        Assert.Equal("OVERRIDE Ana Pérez", Assert.Single(result.Items).RenderedBody);
    }

    [Fact]
    public async Task Auto_selecciona_la_plantilla_que_coincide_con_el_indicador()
    {
        var patient = BuildPatient();
        var alert = BuildAlert(patient.Id);
        var generic = BuildTemplate(NotificationChannel.sms, "GENERICA");
        var match = BuildTemplate(NotificationChannel.sms, "MATCH [valor]", indicator: "ORP");
        var (handler, _, _, _, _) = BuildHandler([alert], patient, [generic, match]);

        var result = await handler.Handle(
            new NotifyAlertsCommand(new NotifyAlertsRequest([alert.Id], [NotificationChannel.sms], Preview: true)),
            CancellationToken.None
        );

        Assert.Equal("MATCH 4.2", Assert.Single(result.Items).RenderedBody);
    }

    private static (
        NotifyAlertsCommandHandler Handler,
        IHealthTestNotificationRepository NotificationRepository,
        IHealthTestRepository HealthTestRepository,
        ISmsSender SmsSender,
        ICommunityMessageSender CommunitySender
    ) BuildHandler(
        IReadOnlyList<HealthTestAlert> alerts,
        PatientProfile patient,
        IReadOnlyList<HealthTestNotificationTemplate>? templates = null
    )
    {
        var healthTestRepository = Substitute.For<IHealthTestRepository>();
        var notificationRepository = Substitute.For<IHealthTestNotificationRepository>();

        notificationRepository
            .ListAlertsByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(alerts);
        notificationRepository
            .GetPatientsByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, PatientProfile> { [patient.Id] = patient });
        notificationRepository
            .ListTemplatesAsync(
                Arg.Any<NotificationChannel?>(),
                Arg.Any<string?>(),
                Arg.Any<bool?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(templates ?? []);

        var smsSender = Substitute.For<ISmsSender>();
        smsSender.Provider.Returns("noop");
        var communitySender = Substitute.For<ICommunityMessageSender>();
        communitySender.Provider.Returns("community");

        var handler = new NotifyAlertsCommandHandler(
            healthTestRepository,
            notificationRepository,
            new HealthTestTemplateRenderer(),
            smsSender,
            communitySender,
            NullLogger<NotifyAlertsCommandHandler>.Instance
        );

        return (handler, notificationRepository, healthTestRepository, smsSender, communitySender);
    }

    private static PatientProfile BuildPatient(
        string? phone = "3001234567",
        string? countryCode = "57",
        bool hasCommunityAccount = true
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            FirstName = "Ana",
            LastName = "Pérez",
            PhoneNumber = phone,
            PhoneCountryCode = countryCode,
            UserId = hasCommunityAccount ? Guid.NewGuid() : null,
            Status = "Activo",
        };

    private static HealthTestAlert BuildAlert(Guid patientId) =>
        new()
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            Severity = HealthTestSeverity.high,
            Title = "Resultado ORP elevado",
            Status = HealthTestAlertStatus.active,
            CreatedAt = DateTime.UtcNow,
            Result = new HealthTestResult
            {
                Code = "ORP",
                Label = "ORP",
                Value = 4.2m,
                ResultType = HealthTestResultType.indicator,
            },
        };

    private static HealthTestNotificationTemplate BuildTemplate(
        NotificationChannel channel,
        string body,
        string? indicator = null,
        HealthTestSeverity? severity = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = $"T{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            Name = "Plantilla de prueba",
            Channel = channel,
            BodyTemplate = body,
            IndicatorCode = indicator,
            Severity = severity,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
}
