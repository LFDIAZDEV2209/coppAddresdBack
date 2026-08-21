using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Caso de uso de finalización de sesión (EndSessionCommandHandler): finaliza la
/// sesión activa, completa la sala (best-effort) y pasa la cita a Completed.
/// Idempotente cuando no hay sesión activa.
/// </summary>
public class EndSessionHandlerTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeVideoProvider _videoProvider = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly EndSessionCommandHandler _handler;

    public EndSessionHandlerTests()
    {
        _handler = new EndSessionCommandHandler(
            _appointments, _videoProvider, _referenceData, NullLogger<EndSessionCommandHandler>.Instance);
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(userId: TestData.UserId);
        _referenceData.UserToProfessional[TestData.UserId] = TestData.ProfessionalId;
        _referenceData.Patients[TestData.PatientId] = TestData.Patient();
        _referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();
    }

    private TelemedicineAppointment AddWithActiveSession()
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.InProgress);
        var room = new VirtualRoom
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            Provider = "twilio",
            ProviderRoomName = $"apt-{appointment.Id:N}",
            ProviderRoomSid = "RM123",
        };
        appointment.Room = room;
        appointment.Sessions.Add(new TelemedicineSession
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            RoomId = room.Id,
            Status = TelemedicineSessionStatus.Active,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-20),
        });
        _appointments.Items.Add(appointment);
        return appointment;
    }

    [Fact]
    public async Task Handle_ConSesionActiva_CompletaCita()
    {
        var appointment = AddWithActiveSession();
        var command = new EndSessionCommand(appointment.Id, "Consulta finalizada", TestData.UserId, false);

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AppointmentStatus.Completed, dto.Status);
        var session = Assert.Single(appointment.Sessions);
        Assert.Equal(TelemedicineSessionStatus.Ended, session.Status);
        Assert.Equal(TestData.UserId, session.EndedBy);
        Assert.NotNull(session.DurationSeconds);
        Assert.Equal(VirtualRoomStatus.Ended, appointment.Room!.Status);
        Assert.Equal(1, _videoProvider.CompleteRoomCalls);
    }

    [Fact]
    public async Task Handle_SinSesionActiva_Idempotente()
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.InProgress);
        _appointments.Items.Add(appointment);
        var command = new EndSessionCommand(appointment.Id, null, TestData.UserId, false);

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AppointmentStatus.InProgress, dto.Status);
        Assert.Equal(0, _videoProvider.CompleteRoomCalls);
    }

    [Fact]
    public async Task Handle_FalloDelProveedor_NoBloqueaFinalizacion()
    {
        var appointment = AddWithActiveSession();
        _videoProvider.CompleteRoomThrows = true;
        var command = new EndSessionCommand(appointment.Id, null, TestData.UserId, false);

        var dto = await _handler.Handle(command, CancellationToken.None);

        // Best-effort: el fallo de Twilio no impide completar la consulta.
        Assert.Equal(AppointmentStatus.Completed, dto.Status);
        Assert.Equal(VirtualRoomStatus.Ended, appointment.Room!.Status);
        Assert.Equal(1, _videoProvider.CompleteRoomCalls);
    }

    [Fact]
    public async Task Handle_PacienteNoPuedeFinalizar_LanzaForbidden()
    {
        var appointment = AddWithActiveSession();
        _referenceData.Patients[TestData.PatientId] = TestData.Patient();
        _referenceData.UserToPatient[TestData.PatientUserId] = TestData.PatientId;
        var command = new EndSessionCommand(appointment.Id, null, TestData.PatientUserId, false);

        await Assert.ThrowsAsync<ForbiddenException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Equal(AppointmentStatus.InProgress, appointment.Status);
    }

    [Fact]
    public async Task Handle_CitaInexistente_LanzaNotFound()
    {
        var command = new EndSessionCommand(Guid.NewGuid(), null, TestData.UserId, false);

        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
