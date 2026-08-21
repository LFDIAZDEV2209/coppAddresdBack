using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Caso de uso de reprogramación (RescheduleTelemedicineAppointmentCommandHandler):
/// solo citas Confirmed, límite de reprogramaciones, sin solapamiento, historial
/// append-only y alerta AppointmentRescheduled.
/// </summary>
public class RescheduleAppointmentHandlerTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly FakeAlertRepository _alerts = new();
    private readonly RescheduleTelemedicineAppointmentCommandHandler _handler;

    public RescheduleAppointmentHandlerTests()
    {
        _handler = new RescheduleTelemedicineAppointmentCommandHandler(
            _appointments, _referenceData, _settings, _alerts);
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(userId: TestData.UserId);
        _referenceData.Patients[TestData.PatientId] = TestData.Patient();
        _referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();
    }

    [Fact]
    public async Task Handle_Valido_ActualizaHorarioIncrementaYRegistraHistorial()
    {
        var fromStart = DateTimeOffset.UtcNow.AddDays(1);
        var appointment = TestData.Appointment(start: fromStart);
        _appointments.Items.Add(appointment);
        var newStart = DateTimeOffset.UtcNow.AddDays(2);
        var command = new RescheduleTelemedicineAppointmentCommand(
            appointment.Id, newStart, null, "Cambio de horario", RescheduleRequestedBy.Patient, TestData.UserId);

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(newStart.ToUniversalTime(), dto.ScheduledStart);
        Assert.Equal(1, dto.RescheduleCount);
        var reschedule = Assert.Single(appointment.Reschedules);
        Assert.Equal(fromStart.ToUniversalTime(), reschedule.FromStart);
        Assert.Equal(newStart.ToUniversalTime(), reschedule.ToStart);
        var alert = Assert.Single(_alerts.Items);
        Assert.Equal(AlertType.AppointmentRescheduled, alert.Type);
    }

    [Fact]
    public async Task Handle_CitaNoConfirmada_LanzaViolacion()
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.Requested);
        _appointments.Items.Add(appointment);
        var command = new RescheduleTelemedicineAppointmentCommand(
            appointment.Id, DateTimeOffset.UtcNow.AddDays(2), null, null, RescheduleRequestedBy.Patient, TestData.UserId);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None));
        Assert.Empty(appointment.Reschedules);
    }

    [Fact]
    public async Task Handle_LimiteDeReprogramaciones_LanzaViolacion()
    {
        var appointment = TestData.Appointment(rescheduleCount: _settings.Settings.MaxReschedules);
        _appointments.Items.Add(appointment);
        var command = new RescheduleTelemedicineAppointmentCommand(
            appointment.Id, DateTimeOffset.UtcNow.AddDays(2), null, null, RescheduleRequestedBy.Patient, TestData.UserId);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None));
        Assert.Equal(_settings.Settings.MaxReschedules, appointment.RescheduleCount);
    }

    [Fact]
    public async Task Handle_NuevoHorarioConSolapamiento_LanzaViolacion()
    {
        var appointment = TestData.Appointment(start: DateTimeOffset.UtcNow.AddDays(1));
        _appointments.Items.Add(appointment);
        var conflicting = TestData.Appointment(start: DateTimeOffset.UtcNow.AddDays(2));
        _appointments.Items.Add(conflicting);
        var command = new RescheduleTelemedicineAppointmentCommand(
            appointment.Id, conflicting.ScheduledStart, null, null, RescheduleRequestedBy.Patient, TestData.UserId);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None));
        // La verificación de solapamiento ocurre ANTES de mutar: sin historial ni incremento.
        Assert.Empty(appointment.Reschedules);
        Assert.Equal(0, appointment.RescheduleCount);
    }

    [Fact]
    public async Task Handle_CitaInexistente_LanzaNotFound()
    {
        var command = new RescheduleTelemedicineAppointmentCommand(
            Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(2), null, null, RescheduleRequestedBy.Patient, TestData.UserId);

        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
