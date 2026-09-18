using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Barrido de recordatorios F2: ventanas de 24 h y 1 h, canales por ventana
/// (push; push+SMS para el paciente a 1 h; push al profesional a 1 h),
/// deduplicación por <c>(appointment_id, kind)</c>, settings deshabilitados y
/// best-effort ante backend caído (no registra → reintenta).
/// </summary>
public class AppointmentReminderSweeperTests
{
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeNotificationDispatchRepository _dispatches = new();
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly FakeTelemedicineNotifier _notifier = new();

    public AppointmentReminderSweeperTests()
    {
        _referenceData.Patients[TestData.PatientId] = TestData.Patient(
            userId: TestData.PatientUserId
        );
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(
            userId: TestData.UserId
        );
    }

    private AppointmentReminderSweeper CreateSweeper() =>
        new(
            _appointments,
            _dispatches,
            _referenceData,
            _settings,
            _notifier,
            NullLogger<AppointmentReminderSweeper>.Instance
        );

    private Appointment Arrange(
        DateTimeOffset start,
        AppointmentStatus status = AppointmentStatus.Confirmed
    )
    {
        var appointment = TestData.Appointment(status: status, start: start);
        _appointments.Items.Add(appointment);
        return appointment;
    }

    [Fact]
    public async Task Sweep_PrimerRecordatorio_EnviaPushUnaVezYDeduplica()
    {
        var now = DateTimeOffset.UtcNow;
        var appointment = Arrange(now.AddHours(12));
        var sweeper = CreateSweeper();

        var first = await sweeper.SweepAsync(now);
        var second = await sweeper.SweepAsync(now);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        var notification = Assert.Single(_notifier.Sent);
        Assert.Equal(TestData.PatientUserId, notification.UserId);
        Assert.Equal(
            new[] { TelemedicineNotificationChannel.Push },
            notification.Channels
        );
        Assert.Equal(appointment.Id.ToString(), notification.Data!["appointmentId"]);
        var dispatch = Assert.Single(_dispatches.Items);
        Assert.Equal(NotificationDispatchKind.Reminder24h, dispatch.Kind);
        Assert.Equal(now.UtcDateTime, dispatch.SentAt);
    }

    [Fact]
    public async Task Sweep_SegundoRecordatorio_EnviaPushYSmsAlPacienteYPushAlProfesional()
    {
        var now = DateTimeOffset.UtcNow;
        Arrange(now.AddMinutes(30));

        var sent = await CreateSweeper().SweepAsync(now);

        // Dos notificaciones (cada una con sus canales): paciente push+SMS y
        // profesional push; el push+SMS del paciente es UN solo envío.
        Assert.Equal(2, sent);
        var patientNotification = Assert.Single(
            _notifier.Sent,
            n => n.UserId == TestData.PatientUserId
        );
        Assert.Equal(
            new[] { TelemedicineNotificationChannel.Push, TelemedicineNotificationChannel.Sms },
            patientNotification.Channels
        );
        var professionalNotification = Assert.Single(
            _notifier.Sent,
            n => n.UserId == TestData.UserId
        );
        Assert.Equal(
            new[] { TelemedicineNotificationChannel.Push },
            professionalNotification.Channels
        );
        Assert.Contains(_dispatches.Items, d => d.Kind == NotificationDispatchKind.Reminder1h);
        Assert.Contains(
            _dispatches.Items,
            d => d.Kind == NotificationDispatchKind.ProfessionalReminder1h
        );
        // La cita ya entró a la ventana corta: no recibe además el aviso de 24 h.
        Assert.DoesNotContain(
            _notifier.Sent,
            n => n.DedupeKey!.Contains("reminder-24h", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task Sweep_NotificacionesDeshabilitadas_NoEnvia()
    {
        var now = DateTimeOffset.UtcNow;
        _settings.Settings.NotificationsEnabled = false;
        Arrange(now.AddMinutes(30));

        var sent = await CreateSweeper().SweepAsync(now);

        Assert.Equal(0, sent);
        Assert.Empty(_notifier.Sent);
        Assert.Empty(_dispatches.Items);
    }

    [Fact]
    public async Task Sweep_CitaNoConfirmada_NoEnvia()
    {
        var now = DateTimeOffset.UtcNow;
        Arrange(now.AddMinutes(30), AppointmentStatus.InProgress);
        Arrange(now.AddHours(12), AppointmentStatus.Cancelled);

        var sent = await CreateSweeper().SweepAsync(now);

        Assert.Equal(0, sent);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task Sweep_BackendNoAcepta_NoRegistraYReintenta()
    {
        var now = DateTimeOffset.UtcNow;
        _notifier.Accepted = false;
        Arrange(now.AddMinutes(30));
        var sweeper = CreateSweeper();

        var first = await sweeper.SweepAsync(now);
        Assert.Equal(0, first);
        Assert.Empty(_dispatches.Items);
        Assert.Equal(2, _notifier.Sent.Count); // intentó, pero no se registró

        _notifier.Accepted = true;
        var second = await sweeper.SweepAsync(now);

        Assert.Equal(2, second);
        Assert.Equal(2, _dispatches.Items.Count);
    }

    [Fact]
    public async Task Sweep_PacienteSinUsuarioDeAuth_OmiteAlPacienteYNotificaAlProfesional()
    {
        var now = DateTimeOffset.UtcNow;
        _referenceData.Patients.Remove(TestData.PatientId);
        Arrange(now.AddMinutes(30));

        var sent = await CreateSweeper().SweepAsync(now);

        Assert.Equal(1, sent);
        var notification = Assert.Single(_notifier.Sent);
        Assert.Equal(TestData.UserId, notification.UserId);
    }

    [Fact]
    public async Task Sweep_SegundoRecordatorioDeshabilitado_EnviaSoloElPrimero()
    {
        var now = DateTimeOffset.UtcNow;
        _settings.Settings.ReminderSecondHoursBefore = 0; // deshabilitado
        Arrange(now.AddHours(12));

        var sent = await CreateSweeper().SweepAsync(now);

        Assert.Equal(1, sent);
        var dispatch = Assert.Single(_dispatches.Items);
        Assert.Equal(NotificationDispatchKind.Reminder24h, dispatch.Kind);
    }

    [Fact]
    public async Task Sweep_SinVentanaSms_EnviaSoloPush()
    {
        var now = DateTimeOffset.UtcNow;
        _settings.Settings.SmsReminderHoursBefore = 0; // SMS fuera de ventana
        Arrange(now.AddMinutes(30));

        await CreateSweeper().SweepAsync(now);

        var patientNotification = Assert.Single(
            _notifier.Sent,
            n => n.UserId == TestData.PatientUserId
        );
        Assert.Equal(
            new[] { TelemedicineNotificationChannel.Push },
            patientNotification.Channels
        );
    }

    [Fact]
    public async Task Sweep_PrimerRecordatorioFueraDeSuVentanaConfigurada_NoEnvia()
    {
        var now = DateTimeOffset.UtcNow;
        _settings.Settings.ReminderFirstHoursBefore = 6;
        Arrange(now.AddHours(12));

        var sent = await CreateSweeper().SweepAsync(now);

        Assert.Equal(0, sent);
        Assert.Empty(_notifier.Sent);
    }
}
