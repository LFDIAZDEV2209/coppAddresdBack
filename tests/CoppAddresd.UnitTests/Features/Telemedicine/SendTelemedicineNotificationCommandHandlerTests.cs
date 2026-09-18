using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Telemedicine;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Telemedicine;

/// <summary>
/// Tests unitarios del handler del endpoint interno de notificaciones de
/// Telemedicina (F2) con fakes en memoria (NSubstitute): resolución del
/// destinatario (paciente/empleado por <c>userId</c>), fan-out push,
/// eliminación de tokens obsoletos, degradación de FCM/SMS y dedupe por
/// <c>dedupeKey</c>. Nunca hay llamadas reales a FCM/Twilio.
/// </summary>
public sealed class SendTelemedicineNotificationCommandHandlerTests
{
    private sealed record Fixture(
        SendTelemedicineNotificationCommandHandler Handler,
        IDeviceTokenRepository Tokens,
        IFcmClient Fcm,
        ISmsSender Sms,
        IPatientRepository Patients,
        IEmployeeRepository Employees,
        INotificationDedupeRepository Dedupe);

    private static Fixture Build()
    {
        var tokens = Substitute.For<IDeviceTokenRepository>();
        var fcm = Substitute.For<IFcmClient>();
        var sms = Substitute.For<ISmsSender>();
        var patients = Substitute.For<IPatientRepository>();
        var employees = Substitute.For<IEmployeeRepository>();
        var dedupe = Substitute.For<INotificationDedupeRepository>();

        // Defaults "felices": FCM entrega, SMS configurado y exitoso, dedupe
        // nueva y un token registrado.
        fcm.SendAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(new FcmSendResult(FcmSendStatus.Sent));
        sms.IsConfigured.Returns(true);
        sms.Provider.Returns("twilio");
        sms.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmsSendResult(true, "SM1", null));
        tokens.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<DeviceToken> { Token("token-1") });
        patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PatientProfile?>(null));
        employees.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Employee?>(null));

        var handler = new SendTelemedicineNotificationCommandHandler(
            tokens, fcm, sms, patients, employees, dedupe,
            NullLogger<SendTelemedicineNotificationCommandHandler>.Instance);

        return new Fixture(handler, tokens, fcm, sms, patients, employees, dedupe);
    }

    private static DeviceToken Token(string value) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Token = value,
        Platform = "android",
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static PatientProfile Patient(string? countryCode, string? phone) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        FirstName = "Ana",
        LastName = "Pérez",
        PhoneCountryCode = countryCode,
        PhoneNumber = phone,
    };

    private static Employee Employee(string? countryCode, string? phone) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        FirstName = "Luis",
        LastName = "Mora",
        PhoneCountryCode = countryCode,
        PhoneNumber = phone,
    };

    private static SendTelemedicineNotificationCommand Command(
        Guid? userId = null,
        IReadOnlyList<string>? channels = null,
        Dictionary<string, string>? data = null,
        string? dedupeKey = null)
        => new(
            userId ?? Guid.NewGuid(),
            "Recordatorio de tu cita",
            "Tu consulta es mañana a las 10:00.",
            channels ?? ["Push"],
            data,
            dedupeKey);

    // =====================================================================
    // Push
    // =====================================================================

    [Fact]
    public async Task Push_hace_fanout_a_todos_los_tokens_y_marca_sent()
    {
        var f = Build();
        f.Tokens.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<DeviceToken> { Token("token-1"), Token("token-2") });

        var result = await f.Handler.Handle(Command(), CancellationToken.None);

        Assert.Equal("sent", result.Push);
        Assert.Equal("skipped", result.Sms);
        await f.Fcm.Received(2).SendAsync(
            Arg.Any<string>(), "Recordatorio de tu cita", "Tu consulta es mañana a las 10:00.",
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Push_propaga_el_data_del_deep_link()
    {
        var f = Build();
        var data = new Dictionary<string, string>
        {
            ["appointmentId"] = "6f2f4f34-2d90-4b2d-9d0a-6f9a1c2b3d4e",
            ["screen"] = "room",
        };

        await f.Handler.Handle(Command(data: data), CancellationToken.None);

        await f.Fcm.Received(1).SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Is<Dictionary<string, string>?>(d =>
                d != null
                && d["appointmentId"] == "6f2f4f34-2d90-4b2d-9d0a-6f9a1c2b3d4e"
                && d["screen"] == "room"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Push_sin_tokens_marca_skipped()
    {
        var f = Build();
        f.Tokens.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<DeviceToken>());

        var result = await f.Handler.Handle(Command(), CancellationToken.None);

        Assert.Equal("skipped", result.Push);
        await f.Fcm.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task Push_con_fcm_deshabilitado_marca_disabled()
    {
        var f = Build();
        f.Fcm.SendAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(new FcmSendResult(FcmSendStatus.Disabled));

        var result = await f.Handler.Handle(Command(), CancellationToken.None);

        Assert.Equal("disabled", result.Push);
    }

    [Fact]
    public async Task Push_con_token_obsoleto_lo_elimina_y_marca_failed()
    {
        var f = Build();
        f.Fcm.SendAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(new FcmSendResult(FcmSendStatus.TokenInvalid, "UNREGISTERED", "gone"));

        var result = await f.Handler.Handle(Command(), CancellationToken.None);

        Assert.Equal("failed", result.Push);
        await f.Tokens.Received(1).DeleteByTokenAsync("token-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Push_con_error_de_fcm_marca_failed_sin_lanzar()
    {
        var f = Build();
        f.Fcm.SendAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(new FcmSendResult(FcmSendStatus.Error, "500", "boom"));

        var result = await f.Handler.Handle(Command(), CancellationToken.None);

        Assert.Equal("failed", result.Push);
    }

    [Fact]
    public async Task Push_con_excepcion_del_cliente_marca_failed_sin_lanzar()
    {
        var f = Build();
        f.Fcm.SendAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<FcmSendResult>(new InvalidOperationException("FCM caído")));

        var result = await f.Handler.Handle(Command(), CancellationToken.None);

        Assert.Equal("failed", result.Push);
    }

    // =====================================================================
    // SMS y resolución del teléfono
    // =====================================================================

    [Fact]
    public async Task Sms_resuelve_el_telefono_del_paciente_y_marca_sent()
    {
        var f = Build();
        f.Patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PatientProfile?>(Patient("57", "3053924819")));

        var result = await f.Handler.Handle(Command(channels: ["Sms"]), CancellationToken.None);

        Assert.Equal("skipped", result.Push);
        Assert.Equal("sent", result.Sms);
        await f.Sms.Received(1).SendAsync(
            "+573053924819", "Tu consulta es mañana a las 10:00.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sms_con_numero_ya_en_e164_no_duplica_el_codigo_de_pais()
    {
        var f = Build();
        f.Patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PatientProfile?>(Patient("57", "+57 305 392 4819")));

        await f.Handler.Handle(Command(channels: ["Sms"]), CancellationToken.None);

        await f.Sms.Received(1).SendAsync(
            "+573053924819", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sms_sin_telefono_marca_skipped_y_no_llama_al_proveedor()
    {
        var f = Build();

        var result = await f.Handler.Handle(Command(channels: ["Sms"]), CancellationToken.None);

        Assert.Equal("skipped", result.Sms);
        await f.Sms.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }

    [Fact]
    public async Task Sms_se_resuelve_del_empleado_cuando_no_hay_perfil_de_paciente()
    {
        var f = Build();
        f.Employees.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Employee?>(Employee("1", "5765550100")));

        var result = await f.Handler.Handle(Command(channels: ["Sms"]), CancellationToken.None);

        Assert.Equal("sent", result.Sms);
        await f.Sms.Received(1).SendAsync(
            "+15765550100", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sms_no_configurado_marca_disabled_sin_llamar_al_proveedor()
    {
        var f = Build();
        f.Sms.IsConfigured.Returns(false);
        f.Patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PatientProfile?>(Patient("57", "3053924819")));

        var result = await f.Handler.Handle(Command(channels: ["Sms"]), CancellationToken.None);

        Assert.Equal("disabled", result.Sms);
        await f.Sms.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }

    [Fact]
    public async Task Sms_con_resultado_fallido_marca_failed()
    {
        var f = Build();
        f.Sms.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmsSendResult(false, null, "twilio:21211"));
        f.Patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PatientProfile?>(Patient("57", "3053924819")));

        var result = await f.Handler.Handle(Command(channels: ["Sms"]), CancellationToken.None);

        Assert.Equal("failed", result.Sms);
    }

    [Fact]
    public async Task Sms_con_excepcion_del_proveedor_marca_failed_sin_lanzar()
    {
        var f = Build();
        f.Sms.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<SmsSendResult>(new InvalidOperationException("proveedor caído")));
        f.Patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PatientProfile?>(Patient("57", "3053924819")));

        var result = await f.Handler.Handle(Command(channels: ["Sms"]), CancellationToken.None);

        Assert.Equal("failed", result.Sms);
    }

    [Fact]
    public async Task Canales_no_solicitados_responden_skipped()
    {
        var f = Build();

        var result = await f.Handler.Handle(Command(channels: ["Push"]), CancellationToken.None);

        Assert.Equal("sent", result.Push);
        Assert.Equal("skipped", result.Sms);
        await f.Sms.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }

    [Fact]
    public async Task Canales_acepta_Push_y_Sms_case_insensitive()
    {
        var f = Build();
        f.Patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PatientProfile?>(Patient("57", "3053924819")));

        var result = await f.Handler.Handle(
            Command(channels: ["PUSH", "Sms"]), CancellationToken.None);

        Assert.Equal("sent", result.Push);
        Assert.Equal("sent", result.Sms);
    }

    // =====================================================================
    // Dedupe
    // =====================================================================

    private static NotificationDedupeKey Previous(string? push, string? sms) => new()
    {
        Id = Guid.NewGuid(),
        DedupeKey = "appointment:1:reminder_1h",
        UserId = Guid.NewGuid(),
        PushStatus = push,
        SmsStatus = sms,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Dedupe_ya_enviada_no_reenvia_y_devuelve_sent()
    {
        var f = Build();
        f.Dedupe.GetByKeyAsync("appointment:1:reminder_1h", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<NotificationDedupeKey?>(Previous("sent", "sent")));

        var result = await f.Handler.Handle(
            Command(channels: ["Push", "Sms"], dedupeKey: "appointment:1:reminder_1h"),
            CancellationToken.None);

        Assert.Equal("sent", result.Push);
        Assert.Equal("sent", result.Sms);
        await f.Fcm.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default, default);
        await f.Sms.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }

    [Fact]
    public async Task Dedupe_con_canal_fallido_permite_reintento_sin_duplicar_el_enviado()
    {
        var f = Build();
        f.Dedupe.GetByKeyAsync("appointment:1:reminder_1h", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<NotificationDedupeKey?>(Previous(push: "sent", sms: "failed")));
        f.Patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PatientProfile?>(Patient("57", "3053924819")));

        var result = await f.Handler.Handle(
            Command(channels: ["Push", "Sms"], dedupeKey: "appointment:1:reminder_1h"),
            CancellationToken.None);

        Assert.Equal("sent", result.Push);
        Assert.Equal("sent", result.Sms);
        // El push ya entregado no se reenvía; el sms fallido sí se reintenta.
        await f.Fcm.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default, default);
        await f.Sms.Received(1).SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await f.Dedupe.Received(1).UpsertAsync(
            "appointment:1:reminder_1h", Arg.Any<Guid>(), "sent", "sent", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dedupe_primera_llamada_envia_y_registra_el_estado()
    {
        var f = Build();
        var userId = Guid.NewGuid();

        var result = await f.Handler.Handle(
            Command(userId: userId, dedupeKey: " appointment:1:reminder_1h ", channels: ["Push"]),
            CancellationToken.None);

        Assert.Equal("sent", result.Push);
        await f.Dedupe.Received(1).UpsertAsync(
            "appointment:1:reminder_1h", userId, "sent", "skipped", Arg.Any<CancellationToken>());
        await f.Fcm.Received(1).SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_dedupeKey_no_consulta_ni_registra_la_clave()
    {
        var f = Build();

        await f.Handler.Handle(Command(dedupeKey: null), CancellationToken.None);

        await f.Dedupe.DidNotReceiveWithAnyArgs().GetByKeyAsync(default!, default);
        await f.Dedupe.DidNotReceiveWithAnyArgs()
            .UpsertAsync(default!, default, default, default, default);
    }
}
