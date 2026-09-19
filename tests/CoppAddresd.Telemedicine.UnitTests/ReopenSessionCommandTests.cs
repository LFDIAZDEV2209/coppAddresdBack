using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Caso de uso de reapertura de consulta (ReopenSessionCommandHandler):
/// autorización del profesional asignado o supervisor con permiso, gracia de
/// 60 minutos desde la finalización, solo citas Completed y creación de una
/// sala nueva en el proveedor (la anterior quedó cerrada).
/// </summary>
public class ReopenSessionCommandTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeRoomRepository _rooms = new();
    private readonly FakeVideoProvider _videoProvider = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly ReopenSessionCommandHandler _handler;

    public ReopenSessionCommandTests()
    {
        _handler = new ReopenSessionCommandHandler(
            _appointments,
            _rooms,
            _videoProvider,
            _referenceData,
            _settings,
            TestOptions.Create(),
            NullLogger<ReopenSessionCommandHandler>.Instance);
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(
            userId: TestData.UserId);
        _referenceData.UserToProfessional[TestData.UserId] = TestData.ProfessionalId;
    }

    private Appointment AddCompleted(TimeSpan? completedAgo = null)
    {
        var appointment = TestData.Appointment(
            status: AppointmentStatus.Completed,
            start: DateTimeOffset.UtcNow.AddHours(-1));
        appointment.CompletedAt =
            DateTimeOffset.UtcNow - (completedAgo ?? TimeSpan.FromMinutes(5));
        _appointments.Items.Add(appointment);
        return appointment;
    }

    [Fact]
    public async Task Handle_ProfesionalDentroDeLaGracia_ReabreConSalaNueva()
    {
        var appointment = AddCompleted();
        var command = new ReopenSessionCommand(appointment.Id, TestData.UserId, false);

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AppointmentStatus.InProgress, dto.Status);
        Assert.Equal(1, appointment.ReopenCount);
        Assert.NotNull(appointment.ReopenedAt);
        Assert.Null(appointment.CompletedAt);
        Assert.Equal(1, _videoProvider.CreateRoomCalls);

        var room = Assert.Single(_rooms.Rooms);
        Assert.Equal($"apt-{appointment.Id:N}-r1", room.ProviderRoomName);
        Assert.Equal(VirtualRoomStatus.Created, room.Status);
        Assert.Null(room.PatientJoinedAt);

        // La ventana se recalcula sobre la reapertura: abre 10 min antes y
        // cierra la duración de la cita + 15 min (la sala anterior ya expiró).
        var reopenedAt = appointment.ReopenedAt!.Value;
        Assert.Equal(reopenedAt.AddMinutes(-10), room.ScheduledOpenAt);
        Assert.Equal(reopenedAt.AddMinutes(45), room.ScheduledCloseAt);
    }

    [Fact]
    public async Task Handle_SupervisorDentroDeLaGracia_Reabre()
    {
        var appointment = AddCompleted();
        var command = new ReopenSessionCommand(appointment.Id, Guid.NewGuid(), true);

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AppointmentStatus.InProgress, dto.Status);
        Assert.Equal(1, appointment.ReopenCount);
    }

    [Fact]
    public async Task Handle_FueraDeLaGracia_LanzaViolacion()
    {
        var appointment = AddCompleted(completedAgo: TimeSpan.FromMinutes(61));
        var command = new ReopenSessionCommand(appointment.Id, TestData.UserId, false);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None));
        Assert.Equal(0, appointment.ReopenCount);
    }

    [Fact]
    public async Task Handle_CitaNoCompletada_LanzaViolacion()
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.InProgress);
        _appointments.Items.Add(appointment);
        var command = new ReopenSessionCommand(appointment.Id, TestData.UserId, false);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PacienteSinPermiso_LanzaForbidden()
    {
        var appointment = AddCompleted();
        _referenceData.Patients[TestData.PatientId] = TestData.Patient();
        _referenceData.UserToPatient[TestData.PatientUserId] = TestData.PatientId;
        var command = new ReopenSessionCommand(appointment.Id, TestData.PatientUserId, false);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _handler.Handle(command, CancellationToken.None));
        Assert.Equal(0, appointment.ReopenCount);
    }

    [Fact]
    public async Task Handle_SalaPreviaEnded_SeReiniciaConSalaFresca()
    {
        var appointment = AddCompleted();
        _rooms.Rooms.Add(
            new VirtualRoom
            {
                AppointmentId = appointment.Id,
                ProviderRoomSid = "RMOLD",
                ProviderRoomName = $"apt-{appointment.Id:N}",
                Status = VirtualRoomStatus.Ended,
                PatientJoinedAt = DateTimeOffset.UtcNow.AddHours(-1),
            }
        );
        var command = new ReopenSessionCommand(appointment.Id, TestData.UserId, false);

        await _handler.Handle(command, CancellationToken.None);

        var room = Assert.Single(_rooms.Rooms);
        Assert.Equal("RM1", room.ProviderRoomSid);
        Assert.Equal($"apt-{appointment.Id:N}-r1", room.ProviderRoomName);
        Assert.Equal(VirtualRoomStatus.Created, room.Status);
        Assert.Null(room.PatientJoinedAt);
    }

    [Fact]
    public async Task Handle_SalaPreviaConCapacidadVieja_ActualizaCapacidadAlSettings()
    {
        var appointment = AddCompleted();
        _rooms.Rooms.Add(
            new VirtualRoom
            {
                AppointmentId = appointment.Id,
                ProviderRoomSid = "RMOLD",
                ProviderRoomName = $"apt-{appointment.Id:N}",
                Status = VirtualRoomStatus.Ended,
                MaxParticipants = 2,
            }
        );
        var command = new ReopenSessionCommand(appointment.Id, TestData.UserId, false);

        await _handler.Handle(command, CancellationToken.None);

        // La sala nueva del proveedor se crea con el settings vigente (3) y la
        // fila persistida queda actualizada (bug corregido en F3).
        var request = Assert.Single(_videoProvider.CreateRoomRequests);
        Assert.Equal(3, request.MaxParticipants);
        var room = Assert.Single(_rooms.Rooms);
        Assert.Equal(3, room.MaxParticipants);
        Assert.Empty(_videoProvider.RoomMaxParticipantsUpdates);
    }

    [Fact]
    public async Task Handle_SettingsFueraDeRango_LanzaViolacionSinCrearSala()
    {
        var appointment = AddCompleted();
        _settings.Settings = TestData.Settings(maxParticipants: 11);
        var command = new ReopenSessionCommand(appointment.Id, TestData.UserId, false);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        Assert.Equal(0, _videoProvider.CreateRoomCalls);
        Assert.Equal(0, appointment.ReopenCount);
    }
}
