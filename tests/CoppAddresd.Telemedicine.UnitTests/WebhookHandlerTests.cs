using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Application.Features.Telemedicine.Events;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Procesamiento de webhooks del proveedor (ProcessTwilioWebhookCommandHandler):
/// firma, idempotencia (duplicado → rollback de mutaciones), estados de la sala
/// y finalización de la cita vía room-ended.
/// </summary>
public class WebhookHandlerTests
{
    private readonly FakeVideoProvider _videoProvider = new();
    private readonly FakeRoomRepository _rooms = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAlertRepository _alerts = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeMetricsQueue _metrics = new();
    private readonly ProcessTwilioWebhookCommandHandler _handler;

    public WebhookHandlerTests()
    {
        _handler = new ProcessTwilioWebhookCommandHandler(
            _videoProvider, _rooms, _appointments, _referenceData, _alerts, _unitOfWork,
            NullLogger<ProcessTwilioWebhookCommandHandler>.Instance, _metrics);
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(userId: TestData.UserId);
        _referenceData.Patients[TestData.PatientId] = TestData.Patient();
    }

    private static ProcessTwilioWebhookCommand Command(string eventType, string roomSid, string participantSid = "PS1", string identity = "")
        => new(
            "https://x/api/v1/telemedicine/webhooks/twilio",
            "sig",
            new Dictionary<string, string>
            {
                ["EventType"] = eventType,
                ["RoomSid"] = roomSid,
                ["ParticipantSid"] = participantSid,
                ["Identity"] = identity,
            });

    private VirtualRoom AddRoomWithActiveSession(bool withSession = true)
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.InProgress);
        _appointments.Items.Add(appointment);

        var room = new VirtualRoom
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            Provider = "twilio",
            ProviderRoomName = $"apt-{appointment.Id:N}",
            ProviderRoomSid = "RM123",
        };
        if (withSession)
        {
            room.Sessions.Add(new TelemedicineSession
            {
                Id = Guid.NewGuid(),
                AppointmentId = appointment.Id,
                RoomId = room.Id,
                Status = TelemedicineSessionStatus.Active,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-15),
            });
        }
        _rooms.Rooms.Add(room);
        return room;
    }

    [Fact]
    public async Task Handle_FirmaInvalida_DevuelveInvalidSignature()
    {
        _videoProvider.SignatureValid = false;

        var result = await _handler.Handle(Command("room-ended", "RM123"), CancellationToken.None);

        Assert.Equal(WebhookProcessOutcome.InvalidSignature, result.Outcome);
        Assert.Empty(_rooms.WebhookEvents);
        Assert.Equal(0, _unitOfWork.Calls);
    }

    [Fact]
    public async Task Handle_SinEventTypeOSid_DevuelveUnknownEvent()
    {
        var command = new ProcessTwilioWebhookCommand(
            "https://x", "sig", new Dictionary<string, string>());

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(WebhookProcessOutcome.UnknownEvent, result.Outcome);
        Assert.Empty(_rooms.WebhookEvents);
    }

    [Fact]
    public async Task Handle_RoomEnded_CompletaCitaEnProgreso()
    {
        var room = AddRoomWithActiveSession();
        var appointment = _appointments.Items.Single();

        var result = await _handler.Handle(Command("room-ended", "RM123"), CancellationToken.None);

        Assert.Equal(WebhookProcessOutcome.Processed, result.Outcome);
        Assert.Equal(VirtualRoomStatus.Ended, room.Status);
        var session = Assert.Single(room.Sessions);
        Assert.Equal(TelemedicineSessionStatus.Ended, session.Status);
        Assert.Equal("room-ended", session.EndReason);
        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        Assert.Single(_rooms.WebhookEvents);
        // room-ended → alerta SessionEnded al profesional.
        Assert.Single(_alerts.Items, a => a.Type == AlertType.SessionEnded);
    }

    [Fact]
    public async Task Handle_RoomEnded_SinSesionIniciada_NoCompletaCita()
    {
        // Cita Confirmada (nunca se inició sesión): room-ended finaliza la sala pero
        // NO completa la cita (NoShow es una decisión de negocio aparte).
        var appointment = TestData.Appointment(status: AppointmentStatus.Confirmed);
        _appointments.Items.Add(appointment);
        var room = new VirtualRoom
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            Provider = "twilio",
            ProviderRoomName = $"apt-{appointment.Id:N}",
            ProviderRoomSid = "RM123",
        };
        _rooms.Rooms.Add(room);

        var result = await _handler.Handle(Command("room-ended", "RM123"), CancellationToken.None);

        Assert.Equal(WebhookProcessOutcome.Processed, result.Outcome);
        Assert.Equal(VirtualRoomStatus.Ended, room.Status);
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
    }

    [Fact]
    public async Task Handle_EventoDuplicado_DevuelveDuplicateSinDobleMutacion()
    {
        var room = AddRoomWithActiveSession();
        var command = Command("room-ended", "RM123");

        var first = await _handler.Handle(command, CancellationToken.None);
        var second = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(WebhookProcessOutcome.Processed, first.Outcome);
        Assert.Equal(WebhookProcessOutcome.Duplicate, second.Outcome);
        Assert.Single(_rooms.WebhookEvents);
        // La cita se completó una sola vez; una sola alerta.
        Assert.Single(_alerts.Items);
        // Dos intentos transaccionales: el primero procesa, el duplicado aborta en el rollback.
        Assert.Equal(2, _unitOfWork.Calls);
    }

    [Fact]
    public async Task Handle_SalaDesconocida_RegistraSinMutaciones()
    {
        var result = await _handler.Handle(Command("room-ended", "RM-desconocida"), CancellationToken.None);

        Assert.Equal(WebhookProcessOutcome.Processed, result.Outcome);
        Assert.Single(_rooms.WebhookEvents);
        Assert.Empty(_alerts.Items);
    }

    [Fact]
    public async Task Handle_ParticipantConnected_Paciente_MaterializaPatientWaiting()
    {
        var room = AddRoomWithActiveSession();
        // El participante (Identity del token Twilio) es el paciente → PatientWaiting.
        var result = await _handler.Handle(
            Command("participant-connected", "RM123", "PS9", TestData.PatientUserId.ToString()),
            CancellationToken.None);

        Assert.Equal(WebhookProcessOutcome.Processed, result.Outcome);
        Assert.Equal(VirtualRoomStatus.Active, room.Status);
        Assert.Contains(_alerts.Items, a => a.Type == AlertType.PatientWaiting);
    }

    [Fact]
    public async Task Handle_ParticipantConnected_Profesional_MaterializaPatientJoined()
    {
        var room = AddRoomWithActiveSession();
        // El participante es el profesional de la cita → PatientJoined.
        var result = await _handler.Handle(
            Command("participant-connected", "RM123", "PS9", TestData.UserId.ToString()),
            CancellationToken.None);

        Assert.Equal(WebhookProcessOutcome.Processed, result.Outcome);
        Assert.Contains(_alerts.Items, a => a.Type == AlertType.PatientJoined);
    }

    [Fact]
    public async Task Handle_ParticipantDisconnected_Paciente_MaterializaParticipantLeft()
    {
        AddRoomWithActiveSession();
        var result = await _handler.Handle(
            Command("participant-disconnected", "RM123", "PS9", TestData.PatientUserId.ToString()),
            CancellationToken.None);

        Assert.Equal(WebhookProcessOutcome.Processed, result.Outcome);
        Assert.Contains(_alerts.Items, a => a.Type == AlertType.ParticipantLeft);
    }

    [Fact]
    public async Task Handle_EventoDesconocido_RegistraSinEfectoDeDominio()
    {
        AddRoomWithActiveSession();
        var result = await _handler.Handle(Command("room-created", "RM123"), CancellationToken.None);

        Assert.Equal(WebhookProcessOutcome.Processed, result.Outcome);
        Assert.Empty(_alerts.Items);
    }

    // ── F5: deriva corregida — el webhook emite tras el commit y solo una vez ─

    [Fact]
    public async Task Handle_RoomEnded_EmiteCambioDeEstadoYSessionEndedTrasElCommit()
    {
        AddRoomWithActiveSession();

        var result = await _handler.Handle(Command("room-ended", "RM123"), CancellationToken.None);

        Assert.Equal(WebhookProcessOutcome.Processed, result.Outcome);
        // La emisión ocurre después de ExecuteInTransactionAsync (una llamada ya
        // completada), no dentro: el duplicado con rollback no la alcanza.
        Assert.Equal(2, _metrics.Events.Count);
        var status = Assert.IsType<AppointmentStatusChangedMetricEvent>(_metrics.Events[0]);
        Assert.Equal(AppointmentStatus.InProgress, status.OldStatus);
        Assert.Equal(AppointmentStatus.Completed, status.NewStatus);
        var ended = Assert.IsType<SessionEndedMetricEvent>(_metrics.Events[1]);
        Assert.NotNull(ended.DurationSeconds);
    }

    [Fact]
    public async Task Handle_RoomEndedDuplicado_NoEmiteDosVeces()
    {
        AddRoomWithActiveSession();
        var command = Command("room-ended", "RM123");

        await _handler.Handle(command, CancellationToken.None);
        var second = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(WebhookProcessOutcome.Duplicate, second.Outcome);
        // Sigue habiendo exactamente 2 eventos del primer procesamiento.
        Assert.Equal(2, _metrics.Events.Count);
    }

    [Fact]
    public async Task Handle_RoomEndedSinSesionIniciada_NoEmiteMetricas()
    {
        // Cita Confirmada: la sala termina pero la cita no cambia de estado.
        var appointment = TestData.Appointment(status: AppointmentStatus.Confirmed);
        _appointments.Items.Add(appointment);
        _rooms.Rooms.Add(new VirtualRoom
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            Provider = "twilio",
            ProviderRoomName = $"apt-{appointment.Id:N}",
            ProviderRoomSid = "RM123",
        });

        var result = await _handler.Handle(Command("room-ended", "RM123"), CancellationToken.None);

        Assert.Equal(WebhookProcessOutcome.Processed, result.Outcome);
        Assert.Empty(_metrics.Events);
    }
}
