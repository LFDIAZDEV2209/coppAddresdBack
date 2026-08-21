using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Caso de uso de join-token (JoinSessionCommandHandler): autorización del
/// participante, estado y ventana, creación perezosa e idempotente de la sala y
/// generación del token de acceso.
/// </summary>
public class JoinSessionHandlerTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeRoomRepository _rooms = new();
    private readonly FakeVideoProvider _videoProvider = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly JoinSessionCommandHandler _handler;

    public JoinSessionHandlerTests()
    {
        _handler = new JoinSessionCommandHandler(
            _appointments, _rooms, _videoProvider, _referenceData, _settings, TestOptions.Create());
    }

    private TelemedicineAppointment AddConfirmed(DateTimeOffset? start = null)
    {
        // Inicio dentro de la ventana de acceso.
        var appointment = TestData.Appointment(
            status: AppointmentStatus.Confirmed,
            start: start ?? DateTimeOffset.UtcNow.AddMinutes(5));
        _appointments.Items.Add(appointment);
        return appointment;
    }

    [Fact]
    public async Task Handle_ProfesionalValido_CreaSalaYGeneraToken()
    {
        var appointment = AddConfirmed();
        _referenceData.Professionals[appointment.ProfessionalId] = TestData.Professional(userId: TestData.UserId);
        _referenceData.UserToProfessional[TestData.UserId] = appointment.ProfessionalId;
        var command = new JoinSessionCommand(appointment.Id, TestData.UserId, false);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal("fake-access-token", result.Token);
        Assert.Equal(1, _videoProvider.CreateRoomCalls);
        // Sin sesión iniciada aún, la sala no tiene sesión activa.
        Assert.Null(result.Room.ActiveSessionStatus);
        Assert.Single(_rooms.Rooms);
    }

    [Fact]
    public async Task Handle_SalaYaExiste_NoCreaDuplicado()
    {
        var appointment = AddConfirmed();
        _referenceData.Professionals[appointment.ProfessionalId] = TestData.Professional(userId: TestData.UserId);
        _referenceData.UserToProfessional[TestData.UserId] = appointment.ProfessionalId;
        _rooms.Rooms.Add(new VirtualRoom
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            Provider = "twilio",
            ProviderRoomName = $"apt-{appointment.Id:N}",
            ProviderRoomSid = "RM-existente",
        });
        var command = new JoinSessionCommand(appointment.Id, TestData.UserId, false);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal($"apt-{appointment.Id:N}", result.Room.ProviderRoomName);
        Assert.Equal("RM-existente", _rooms.Rooms[0].ProviderRoomSid);
        Assert.Equal(0, _videoProvider.CreateRoomCalls);
        Assert.Single(_rooms.Rooms);
    }

    [Fact]
    public async Task Handle_UsuarioAjeno_LanzaForbidden()
    {
        var appointment = AddConfirmed();
        var command = new JoinSessionCommand(appointment.Id, Guid.NewGuid(), false);

        await Assert.ThrowsAsync<ForbiddenException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Empty(_rooms.Rooms);
    }

    [Fact]
    public async Task Handle_FueraDeVentana_LanzaViolacion()
    {
        var appointment = AddConfirmed(DateTimeOffset.UtcNow.AddHours(5));
        _referenceData.Professionals[appointment.ProfessionalId] = TestData.Professional(userId: TestData.UserId);
        _referenceData.UserToProfessional[TestData.UserId] = appointment.ProfessionalId;
        var command = new JoinSessionCommand(appointment.Id, TestData.UserId, false);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CitaNoIniciable_LanzaViolacion()
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.Completed);
        _appointments.Items.Add(appointment);
        _referenceData.Professionals[appointment.ProfessionalId] = TestData.Professional(userId: TestData.UserId);
        _referenceData.UserToProfessional[TestData.UserId] = appointment.ProfessionalId;
        var command = new JoinSessionCommand(appointment.Id, TestData.UserId, false);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CitaInexistente_LanzaNotFound()
    {
        var command = new JoinSessionCommand(Guid.NewGuid(), TestData.UserId, false);

        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
