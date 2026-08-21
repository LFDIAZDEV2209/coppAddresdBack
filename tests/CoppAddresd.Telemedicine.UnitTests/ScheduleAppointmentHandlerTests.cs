using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Caso de uso de agendamiento directo (ScheduleTelemedicineAppointmentCommandHandler):
/// cita nace Confirmed, sin solapamiento, con alerta NewAppointment al profesional.
/// </summary>
public class ScheduleAppointmentHandlerTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly FakeAlertRepository _alerts = new();
    private readonly ScheduleTelemedicineAppointmentCommandHandler _handler;

    public ScheduleAppointmentHandlerTests()
    {
        _handler = new ScheduleTelemedicineAppointmentCommandHandler(
            _appointments, _referenceData, _settings, _alerts);
        _referenceData.Patients[TestData.PatientId] = TestData.Patient();
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(userId: TestData.UserId);
        _referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();
        _referenceData.Locations[TestData.LocationId] = TestData.Location();
    }

    private static ScheduleTelemedicineAppointmentCommand Command(DateTimeOffset start, int? duration = null)
        => new(TestData.PatientId, TestData.ProfessionalId, TestData.SpecialtyId, TestData.Org, TestData.Clinic,
            TestData.LocationId, start, duration, TestData.UserId);

    [Fact]
    public async Task Handle_Valido_CreaCitaConfirmedYAlerta()
    {
        var command = Command(DateTimeOffset.UtcNow.AddDays(1));

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AppointmentStatus.Confirmed, dto.Status);
        var entity = Assert.Single(_appointments.Items);
        Assert.Equal(30, entity.DurationMinutes);
        Assert.Null(entity.RequestId);
        var alert = Assert.Single(_alerts.Items);
        Assert.Equal(AlertType.NewAppointment, alert.Type);
        Assert.Equal(TestData.UserId, alert.RecipientUserId);
    }

    [Fact]
    public async Task Handle_ConSolapamiento_LanzaViolacion()
    {
        var start = DateTimeOffset.UtcNow.AddDays(1);
        _appointments.Items.Add(TestData.Appointment(start: start));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(Command(start), CancellationToken.None));
        Assert.Single(_appointments.Items);
    }

    [Fact]
    public async Task Handle_PacienteInexistente_LanzaNotFound()
    {
        var command = new ScheduleTelemedicineAppointmentCommand(
            Guid.NewGuid(), TestData.ProfessionalId, TestData.SpecialtyId, TestData.Org, TestData.Clinic,
            TestData.LocationId, DateTimeOffset.UtcNow.AddDays(1), null, TestData.UserId);

        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ProfesionalInexistente_LanzaNotFound()
    {
        var command = new ScheduleTelemedicineAppointmentCommand(
            TestData.PatientId, Guid.NewGuid(), TestData.SpecialtyId, TestData.Org, TestData.Clinic,
            TestData.LocationId, DateTimeOffset.UtcNow.AddDays(1), null, TestData.UserId);

        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
