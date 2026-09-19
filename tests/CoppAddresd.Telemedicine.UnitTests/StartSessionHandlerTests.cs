using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Caso de uso de inicio de sesión (StartSessionCommandHandler): autorización
/// del profesional/supervisor, estado de la cita, ventana, una sesión activa a
/// la vez, elevación perezosa de capacidad (F3) y transición
/// Confirmed → InProgress con creación de sala.
/// </summary>
public class StartSessionHandlerTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeVideoProvider _videoProvider = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly StartSessionCommandHandler _handler;

    public StartSessionHandlerTests()
    {
        _handler = new StartSessionCommandHandler(
            _appointments, _videoProvider, _referenceData, _settings,
            TestOptions.Create(), NullLogger<StartSessionCommandHandler>.Instance);
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(userId: TestData.UserId);
        _referenceData.UserToProfessional[TestData.UserId] = TestData.ProfessionalId;
    }

    private Appointment AddConfirmed(DateTimeOffset? start = null)
    {
        // Inicio dentro de la ventana de acceso (abre 10 min antes, cierra 15 después).
        var appointment = TestData.Appointment(
            status: AppointmentStatus.Confirmed,
            start: start ?? DateTimeOffset.UtcNow.AddMinutes(5));
        _appointments.Items.Add(appointment);
        return appointment;
    }

    [Fact]
    public async Task Handle_Valido_IniciaSesionYVuelveInProgress()
    {
        var appointment = AddConfirmed();
        var command = new StartSessionCommand(appointment.Id, TestData.UserId, false);

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AppointmentStatus.InProgress, dto.Status);
        var session = Assert.Single(appointment.Sessions);
        Assert.Equal(TelemedicineSessionStatus.Active, session.Status);
        Assert.NotNull(appointment.Room);
        Assert.Equal(1, _videoProvider.CreateRoomCalls);
    }

    [Fact]
    public async Task Handle_PacienteNoPuedeIniciar_LanzaForbidden()
    {
        var appointment = AddConfirmed();
        _referenceData.Patients[TestData.PatientId] = TestData.Patient();
        _referenceData.UserToPatient[TestData.PatientUserId] = TestData.PatientId;
        var command = new StartSessionCommand(appointment.Id, TestData.PatientUserId, false);

        await Assert.ThrowsAsync<ForbiddenException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Empty(appointment.Sessions);
    }

    [Fact]
    public async Task Handle_CitaNoIniciable_LanzaViolacion()
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.Requested);
        _appointments.Items.Add(appointment);
        var command = new StartSessionCommand(appointment.Id, TestData.UserId, false);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_FueraDeVentana_LanzaViolacion()
    {
        // Cita dentro de 5 horas: fuera de la ventana de acceso (abre 10 min antes).
        var appointment = AddConfirmed(DateTimeOffset.UtcNow.AddHours(5));
        var command = new StartSessionCommand(appointment.Id, TestData.UserId, false);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DobleInicio_LanzaViolacion()
    {
        var appointment = AddConfirmed();
        var command = new StartSessionCommand(appointment.Id, TestData.UserId, false);
        await _handler.Handle(command, CancellationToken.None);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None));
        Assert.Single(appointment.Sessions);
    }

    [Fact]
    public async Task Handle_SupervisorPuedeIniciar()
    {
        var appointment = AddConfirmed();
        var command = new StartSessionCommand(appointment.Id, Guid.NewGuid(), true);

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AppointmentStatus.InProgress, dto.Status);
    }

    [Fact]
    public async Task Handle_SalaExistenteCapacidadMenor_ElevaEnProveedorYPersiste()
    {
        var appointment = AddConfirmed();
        appointment.Room = new VirtualRoom
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            ProviderRoomSid = "RM-vieja",
            ProviderRoomName = $"apt-{appointment.Id:N}",
            MaxParticipants = 2,
        };
        var command = new StartSessionCommand(appointment.Id, TestData.UserId, false);

        await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(0, _videoProvider.CreateRoomCalls);
        var update = Assert.Single(_videoProvider.RoomMaxParticipantsUpdates);
        Assert.Equal(("RM-vieja", 3), update);
        Assert.Equal(3, appointment.Room.MaxParticipants);
    }

    [Fact]
    public async Task Handle_SalaExistenteCapacidadSuficiente_NoActualizaProveedor()
    {
        var appointment = AddConfirmed();
        appointment.Room = new VirtualRoom
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            ProviderRoomSid = "RM-actual",
            ProviderRoomName = $"apt-{appointment.Id:N}",
            MaxParticipants = 3,
        };

        await _handler.Handle(
            new StartSessionCommand(appointment.Id, TestData.UserId, false), CancellationToken.None);

        Assert.Empty(_videoProvider.RoomMaxParticipantsUpdates);
    }

    [Fact]
    public async Task Handle_SettingsFueraDeRango_LanzaViolacionSinCrearSala()
    {
        var appointment = AddConfirmed();
        _settings.Settings = TestData.Settings(maxParticipants: 11);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(
                new StartSessionCommand(appointment.Id, TestData.UserId, false), CancellationToken.None));

        Assert.Equal(0, _videoProvider.CreateRoomCalls);
        Assert.Null(appointment.Room);
    }
}
