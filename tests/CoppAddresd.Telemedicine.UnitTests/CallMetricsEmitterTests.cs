using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Application.Features.Telemedicine.Events;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Infrastructure.Metrics;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Emisión de métricas de llamada F5: cada handler encola el evento correcto en
/// la cola (fake que captura), con la identidad de la cita y la fecha de agenda
/// (UTC de scheduled_start), y solo en las transiciones que corresponden.
/// </summary>
public class CallMetricsEmitterTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeRoomRepository _rooms = new();
    private readonly FakeVideoProvider _videoProvider = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly FakeMetricsQueue _metrics = new();

    public CallMetricsEmitterTests()
    {
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(
            userId: TestData.UserId);
        _referenceData.Patients[TestData.PatientId] = TestData.Patient();
        _referenceData.UserToProfessional[TestData.UserId] = TestData.ProfessionalId;
        _referenceData.UserToPatient[TestData.PatientUserId] = TestData.PatientId;
    }

    private Appointment AddConfirmed(DateTimeOffset? start = null)
    {
        var appointment = TestData.Appointment(
            status: AppointmentStatus.Confirmed,
            start: start ?? DateTimeOffset.UtcNow.AddMinutes(5));
        _appointments.Items.Add(appointment);
        return appointment;
    }

    private static DateOnly ScheduledDate(Appointment appointment) =>
        DateOnly.FromDateTime(appointment.ScheduledStart.UtcDateTime);

    // ── Join (sala perezosa) ────────────────────────────────────────────────

    [Fact]
    public async Task Join_SinSala_EmiteRoomOpenedConFechaDeAgenda()
    {
        var appointment = AddConfirmed();
        var handler = new JoinSessionCommandHandler(
            _appointments, _rooms, _videoProvider, _referenceData, _settings,
            TestOptions.Create(), NullLogger<JoinSessionCommandHandler>.Instance, _metrics);

        await handler.Handle(
            new JoinSessionCommand(appointment.Id, TestData.UserId, false), CancellationToken.None);

        var e = Assert.IsType<RoomOpenedMetricEvent>(Assert.Single(_metrics.Events));
        Assert.Equal(appointment.Id, e.AppointmentId);
        Assert.Equal(appointment.ProfessionalId, e.ProfessionalId);
        Assert.Equal(appointment.ClinicId, e.ClinicId);
        Assert.Equal(ScheduledDate(appointment), e.ScheduledDate);
    }

    [Fact]
    public async Task Join_SalaExistente_NoEmiteRoomOpened()
    {
        var appointment = AddConfirmed();
        _rooms.Rooms.Add(new VirtualRoom
        {
            AppointmentId = appointment.Id,
            ProviderRoomSid = "RM-existente",
            ProviderRoomName = $"apt-{appointment.Id:N}",
        });
        var handler = new JoinSessionCommandHandler(
            _appointments, _rooms, _videoProvider, _referenceData, _settings,
            TestOptions.Create(), NullLogger<JoinSessionCommandHandler>.Instance, _metrics);

        await handler.Handle(
            new JoinSessionCommand(appointment.Id, TestData.UserId, false), CancellationToken.None);

        Assert.Empty(_metrics.Events);
    }

    // ── Start (sala + sesión) ───────────────────────────────────────────────

    [Fact]
    public async Task Start_SinSala_EmiteRoomOpenedYSessionStarted()
    {
        var appointment = AddConfirmed();
        var handler = new StartSessionCommandHandler(
            _appointments, _videoProvider, _referenceData, _settings,
            TestOptions.Create(), NullLogger<StartSessionCommandHandler>.Instance, _metrics);

        await handler.Handle(
            new StartSessionCommand(appointment.Id, TestData.UserId, false), CancellationToken.None);

        Assert.Equal(2, _metrics.Events.Count);
        var roomOpened = Assert.IsType<RoomOpenedMetricEvent>(_metrics.Events[0]);
        var sessionStarted = Assert.IsType<SessionStartedMetricEvent>(_metrics.Events[1]);
        Assert.Equal(ScheduledDate(appointment), roomOpened.ScheduledDate);
        Assert.Equal(ScheduledDate(appointment), sessionStarted.ScheduledDate);
        Assert.Equal(appointment.ProfessionalId, sessionStarted.ProfessionalId);
    }

    [Fact]
    public async Task Start_SalaExistente_SoloEmiteSessionStarted()
    {
        var appointment = AddConfirmed();
        appointment.Room = new VirtualRoom
        {
            AppointmentId = appointment.Id,
            ProviderRoomSid = "RM-vieja",
            ProviderRoomName = $"apt-{appointment.Id:N}",
            MaxParticipants = 3,
        };
        var handler = new StartSessionCommandHandler(
            _appointments, _videoProvider, _referenceData, _settings,
            TestOptions.Create(), NullLogger<StartSessionCommandHandler>.Instance, _metrics);

        await handler.Handle(
            new StartSessionCommand(appointment.Id, TestData.UserId, false), CancellationToken.None);

        Assert.IsType<SessionStartedMetricEvent>(Assert.Single(_metrics.Events));
    }

    // ── End (sesión + estado) ───────────────────────────────────────────────

    [Fact]
    public async Task End_ConSesionActiva_EmiteSessionEndedConDuracionYCambioDeEstado()
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.InProgress);
        appointment.Sessions.Add(new TelemedicineSession
        {
            AppointmentId = appointment.Id,
            Status = TelemedicineSessionStatus.Active,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-20),
        });
        _appointments.Items.Add(appointment);
        var handler = new EndSessionCommandHandler(
            _appointments, _videoProvider, _referenceData,
            NullLogger<EndSessionCommandHandler>.Instance, _metrics);

        await handler.Handle(
            new EndSessionCommand(appointment.Id, null, TestData.UserId, false), CancellationToken.None);

        Assert.Equal(2, _metrics.Events.Count);
        var status = Assert.IsType<AppointmentStatusChangedMetricEvent>(
            Assert.Single(_metrics.Events, e => e is AppointmentStatusChangedMetricEvent));
        Assert.Equal(AppointmentStatus.InProgress, status.OldStatus);
        Assert.Equal(AppointmentStatus.Completed, status.NewStatus);

        var ended = Assert.IsType<SessionEndedMetricEvent>(
            Assert.Single(_metrics.Events, e => e is SessionEndedMetricEvent));
        Assert.NotNull(ended.DurationSeconds);
        Assert.InRange(ended.DurationSeconds!.Value, 1190, 1260);
        Assert.Equal(ScheduledDate(appointment), ended.ScheduledDate);
    }

    [Fact]
    public async Task End_SinSesionActiva_NoEmiteEventos()
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.InProgress);
        _appointments.Items.Add(appointment);
        var handler = new EndSessionCommandHandler(
            _appointments, _videoProvider, _referenceData,
            NullLogger<EndSessionCommandHandler>.Instance, _metrics);

        await handler.Handle(
            new EndSessionCommand(appointment.Id, null, TestData.UserId, false), CancellationToken.None);

        Assert.Empty(_metrics.Events);
    }

    // ── Reapertura (reutiliza el evento de estado; no cuenta salas) ─────────

    [Fact]
    public async Task Reopen_EmiteCambioDeEstadoCompletedAInProgressSinRoomOpened()
    {
        var appointment = TestData.Appointment(
            status: AppointmentStatus.Completed, start: DateTimeOffset.UtcNow.AddHours(-1));
        appointment.CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        _appointments.Items.Add(appointment);
        var handler = new ReopenSessionCommandHandler(
            _appointments, _rooms, _videoProvider, _referenceData, _settings,
            TestOptions.Create(), NullLogger<ReopenSessionCommandHandler>.Instance, _metrics);

        await handler.Handle(
            new ReopenSessionCommand(appointment.Id, TestData.UserId, false), CancellationToken.None);

        var status = Assert.IsType<AppointmentStatusChangedMetricEvent>(Assert.Single(_metrics.Events));
        Assert.Equal(AppointmentStatus.Completed, status.OldStatus);
        Assert.Equal(AppointmentStatus.InProgress, status.NewStatus);
        Assert.DoesNotContain(_metrics.Events, e => e is RoomOpenedMetricEvent);
    }

    // ── Chat (rol derivado del JWT) ─────────────────────────────────────────

    [Theory]
    [InlineData("Professional")]
    [InlineData("Patient")]
    [InlineData("Supervisor")]
    public async Task Chat_EmiteRolDerivadoDelJwt(string expectedRole)
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.InProgress);
        _appointments.Items.Add(appointment);
        var userId = expectedRole switch
        {
            "Professional" => TestData.UserId,
            "Patient" => TestData.PatientUserId,
            _ => Guid.NewGuid(),
        };
        var hasManagePermission = expectedRole == "Supervisor";
        var handler = new SendRoomChatMessageCommandHandler(
            _appointments, new FakeChatMessageRepository(), _referenceData,
            NullLogger<SendRoomChatMessageCommandHandler>.Instance, _metrics);

        await handler.Handle(
            new SendRoomChatMessageCommand(appointment.Id, "hola", userId, hasManagePermission),
            CancellationToken.None);

        var e = Assert.IsType<ChatMessageSentMetricEvent>(Assert.Single(_metrics.Events));
        Assert.Equal(expectedRole, e.Role);
        Assert.Equal(ScheduledDate(appointment), e.ScheduledDate);
    }

    // ── Derivación de claves/dimensiones del processor ──────────────────────

    [Fact]
    public void MapCounters_RoomOpened_EsRoomsOpenedGeneral()
    {
        var counters = TelemedicineMetricsProcessorHostedService.MapCounters(
            new RoomOpenedMetricEvent(Guid.NewGuid(), Guid.NewGuid(), null, DateOnly.FromDateTime(DateTime.UtcNow)));

        var counter = Assert.Single(counters);
        Assert.Equal("rooms_opened", counter.MetricKey);
        Assert.Equal("general", counter.DimensionKey);
        Assert.Equal(1, counter.Increment);
    }

    [Fact]
    public void MapCounters_SessionStarted_EsSessionsStartedGeneral()
    {
        var counters = TelemedicineMetricsProcessorHostedService.MapCounters(
            new SessionStartedMetricEvent(Guid.NewGuid(), Guid.NewGuid(), null, DateOnly.FromDateTime(DateTime.UtcNow)));

        var counter = Assert.Single(counters);
        Assert.Equal("sessions_started", counter.MetricKey);
        Assert.Equal("general", counter.DimensionKey);
        Assert.Equal(1, counter.Increment);
    }

    [Fact]
    public void MapCounters_SessionEndedConDuracion_SumaSesionYDuracion()
    {
        var counters = TelemedicineMetricsProcessorHostedService.MapCounters(
            new SessionEndedMetricEvent(
                Guid.NewGuid(), Guid.NewGuid(), null, DateOnly.FromDateTime(DateTime.UtcNow), 125));

        Assert.Collection(
            counters,
            c => Assert.Equal(("sessions_ended", "general", 1L), (c.MetricKey, c.DimensionKey, c.Increment)),
            c => Assert.Equal(
                ("session_duration_seconds", "general", 125L),
                (c.MetricKey, c.DimensionKey, c.Increment)));
    }

    [Fact]
    public void MapCounters_SessionEndedSinDuracion_SoloSumaSesion()
    {
        var counters = TelemedicineMetricsProcessorHostedService.MapCounters(
            new SessionEndedMetricEvent(
                Guid.NewGuid(), Guid.NewGuid(), null, DateOnly.FromDateTime(DateTime.UtcNow), null));

        var counter = Assert.Single(counters);
        Assert.Equal(("sessions_ended", "general", 1L), (counter.MetricKey, counter.DimensionKey, counter.Increment));
    }

    [Fact]
    public void MapCounters_Chat_UsaElRolComoDimension()
    {
        var counters = TelemedicineMetricsProcessorHostedService.MapCounters(
            new ChatMessageSentMetricEvent(
                Guid.NewGuid(), Guid.NewGuid(), null, DateOnly.FromDateTime(DateTime.UtcNow), "Patient"));

        var counter = Assert.Single(counters);
        Assert.Equal(("chat_messages_sent", "Patient", 1L), (counter.MetricKey, counter.DimensionKey, counter.Increment));
    }

    [Fact]
    public void MapCounters_EventoViejo_NoProduceContadores()
    {
        var scheduled = new AppointmentScheduledMetricEvent(
            Guid.NewGuid(), Guid.NewGuid(), null, DateOnly.FromDateTime(DateTime.UtcNow), 9,
            AppointmentStatus.Confirmed);

        Assert.Empty(TelemedicineMetricsProcessorHostedService.MapCounters(scheduled));
    }

    [Fact]
    public void IsReopen_SoloCompletedAInProgress()
    {
        Assert.True(TelemedicineMetricsProcessorHostedService.IsReopen(
            AppointmentStatus.Completed, AppointmentStatus.InProgress));
        Assert.False(TelemedicineMetricsProcessorHostedService.IsReopen(
            AppointmentStatus.InProgress, AppointmentStatus.Completed));
        Assert.False(TelemedicineMetricsProcessorHostedService.IsReopen(
            AppointmentStatus.Confirmed, AppointmentStatus.InProgress));
    }
}
