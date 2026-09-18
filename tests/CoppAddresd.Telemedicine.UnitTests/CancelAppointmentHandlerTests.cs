using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Caso de uso de cancelación (CancelAppointmentCommandHandler):
/// estados no cancelables, historial append-only, integridad del encuentro
/// (borrador → Cancelled; Completed preservado) y alerta AppointmentCancelled.
/// </summary>
public class CancelAppointmentHandlerTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeAlertRepository _alerts = new();
    private readonly FakeTelemedicineNotifier _notifier = new();
    private readonly CancelAppointmentCommandHandler _handler;

    public CancelAppointmentHandlerTests()
    {
        _handler = new CancelAppointmentCommandHandler(
            _appointments, _referenceData, _alerts, notifier: _notifier);
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(userId: TestData.UserId);
        _referenceData.Patients[TestData.PatientId] = TestData.Patient(userId: TestData.PatientUserId);
        _referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();
    }

    [Fact]
    public async Task Handle_Valido_CancelaConHistorialYAlerta()
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.Confirmed);
        _appointments.Items.Add(appointment);
        var command = new CancelAppointmentCommand(
            appointment.Id, "Emergencia", CancelledBy.Patient, TestData.UserId);

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AppointmentStatus.Cancelled, dto.Status);
        Assert.Equal("Emergencia", dto.CancellationReason);
        var cancellation = Assert.Single(appointment.Cancellations);
        Assert.Equal(CancelledBy.Patient, cancellation.CancelledBy);
        Assert.Equal(TestData.UserId, cancellation.CancelledByUserId);
        var alert = Assert.Single(_alerts.Items);
        Assert.Equal(AlertType.AppointmentCancelled, alert.Type);
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task Handle_EstadoNoCancelable_LanzaViolacion(AppointmentStatus status)
    {
        var appointment = TestData.Appointment(status: status);
        _appointments.Items.Add(appointment);
        var command = new CancelAppointmentCommand(
            appointment.Id, "Razón", CancelledBy.Professional, TestData.UserId);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None));
        Assert.Empty(appointment.Cancellations);
    }

    [Fact]
    public async Task Handle_BorradorDeEncuentro_SeCancela()
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.InProgress);
        appointment.Encounter = new ClinicalEncounter
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            Status = EncounterStatus.Draft,
            Notes = "Nota inicial",
        };
        _appointments.Items.Add(appointment);
        var command = new CancelAppointmentCommand(
            appointment.Id, "El paciente no puede asistir", CancelledBy.Patient, TestData.UserId);

        await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(EncounterStatus.Cancelled, appointment.Encounter.Status);
    }

    [Fact]
    public async Task Handle_EncuentroCompletado_SePreserva()
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.InProgress);
        appointment.Encounter = new ClinicalEncounter
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            Status = EncounterStatus.Completed,
            Notes = "Registro final",
        };
        _appointments.Items.Add(appointment);
        var command = new CancelAppointmentCommand(
            appointment.Id, "Razón", CancelledBy.Admin, TestData.UserId);

        await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(EncounterStatus.Completed, appointment.Encounter.Status);
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }

    [Fact]
    public async Task Handle_CitaInexistente_LanzaNotFound()
    {
        var command = new CancelAppointmentCommand(
            Guid.NewGuid(), "Razón", CancelledBy.Admin, TestData.UserId);

        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CanceladoPorProfesional_NotificaAlPacienteConLaRazon()
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.Confirmed);
        _appointments.Items.Add(appointment);
        var command = new CancelAppointmentCommand(
            appointment.Id, "Emergencia del profesional", CancelledBy.Professional, TestData.UserId);

        await _handler.Handle(command, CancellationToken.None);

        var notification = Assert.Single(_notifier.Sent);
        Assert.Equal(TestData.PatientUserId, notification.UserId);
        Assert.Contains("Emergencia del profesional", notification.Body);
    }

    [Fact]
    public async Task Handle_CanceladoPorPaciente_NotificaAlProfesional()
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.Confirmed);
        _appointments.Items.Add(appointment);
        var command = new CancelAppointmentCommand(
            appointment.Id, "No puedo asistir", CancelledBy.Patient, TestData.PatientUserId);

        await _handler.Handle(command, CancellationToken.None);

        var notification = Assert.Single(_notifier.Sent);
        Assert.Equal(TestData.UserId, notification.UserId);
        Assert.Contains("No puedo asistir", notification.Body);
    }
}
