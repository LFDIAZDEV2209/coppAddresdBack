using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Caso de uso de confirmación de solicitud (ConfirmTelemedicineRequestCommandHandler):
/// valida el estado de la solicitud, resuelve el slot, crea la cita Confirmed
/// vinculada, marca la solicitud Converted y emite la alerta NewAppointment.
/// </summary>
public class ConfirmRequestHandlerTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeRequestRepository _requests = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly FakeAlertRepository _alerts = new();
    private readonly ConfirmTelemedicineRequestCommandHandler _handler;

    public ConfirmRequestHandlerTests()
    {
        _handler = new ConfirmTelemedicineRequestCommandHandler(
            _requests, _appointments, _referenceData, _settings, _alerts);
        _referenceData.Patients[TestData.PatientId] = TestData.Patient();
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(userId: TestData.UserId);
        _referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();
        _referenceData.Locations[TestData.LocationId] = TestData.Location();
    }

    private TelemedicineRequest AddRequest(AppointmentRequestStatus status = AppointmentRequestStatus.Pending)
    {
        var request = new TelemedicineRequest
        {
            Id = Guid.NewGuid(),
            PatientId = TestData.PatientId,
            OrganizationId = TestData.Org,
            SpecialtyId = TestData.SpecialtyId,
            ProfessionalId = TestData.ProfessionalId,
            ClinicId = TestData.Clinic,
            LocationId = TestData.LocationId,
            Reason = "Dolor abdominal",
            Status = status,
            CreatedBy = TestData.UserId,
        };
        _requests.Items.Add(request);
        return request;
    }

    [Fact]
    public async Task Handle_Valido_CreaCitaConvertedYAlerta()
    {
        var request = AddRequest();
        var command = new ConfirmTelemedicineRequestCommand(
            request.Id, TestData.ProfessionalId, DateTimeOffset.UtcNow.AddDays(1), null, TestData.LocationId, TestData.UserId);

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AppointmentStatus.Confirmed, dto.Status);
        Assert.Equal(request.Id, dto.RequestId);
        Assert.Equal(AppointmentRequestStatus.Converted, request.Status);
        var alert = Assert.Single(_alerts.Items);
        Assert.Equal(AlertType.NewAppointment, alert.Type);
    }

    [Fact]
    public async Task Handle_SolicitudConvertida_LanzaViolacion()
    {
        var request = AddRequest(AppointmentRequestStatus.Converted);
        var command = new ConfirmTelemedicineRequestCommand(
            request.Id, TestData.ProfessionalId, DateTimeOffset.UtcNow.AddDays(1), null, TestData.LocationId, TestData.UserId);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None));
        Assert.Empty(_appointments.Items);
    }

    [Fact]
    public async Task Handle_SolicitudCancelada_LanzaViolacion()
    {
        var request = AddRequest(AppointmentRequestStatus.Cancelled);
        var command = new ConfirmTelemedicineRequestCommand(
            request.Id, TestData.ProfessionalId, DateTimeOffset.UtcNow.AddDays(1), null, TestData.LocationId, TestData.UserId);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ConSolapamiento_LanzaViolacion()
    {
        var request = AddRequest();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        _appointments.Items.Add(TestData.Appointment(start: start));
        var command = new ConfirmTelemedicineRequestCommand(
            request.Id, TestData.ProfessionalId, start, null, TestData.LocationId, TestData.UserId);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None));
        Assert.Equal(AppointmentRequestStatus.Pending, request.Status);
    }

    [Fact]
    public async Task Handle_SolicitudInexistente_LanzaNotFound()
    {
        var command = new ConfirmTelemedicineRequestCommand(
            Guid.NewGuid(), TestData.ProfessionalId, DateTimeOffset.UtcNow.AddDays(1), null, TestData.LocationId, TestData.UserId);

        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
