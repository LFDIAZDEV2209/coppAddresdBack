using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Caso de uso de revisión de solicitud (ReviewTelemedicineRequestCommandHandler):
/// transiciones Pending → Approved y Pending|Approved → Rejected (motivo
/// obligatorio), alcance dual (admin con AdminView o profesional asignado por
/// identidad JWT) y alertas RequestApproved/RequestRejected.
/// </summary>
public class ReviewRequestHandlerTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeRequestRepository _requests = new();
    private readonly FakeAlertRepository _alerts = new();
    private readonly ReviewTelemedicineRequestCommandHandler _handler;

    public ReviewRequestHandlerTests()
    {
        _handler = new ReviewTelemedicineRequestCommandHandler(_requests, _referenceData, _alerts);
        _referenceData.Patients[TestData.PatientId] = TestData.Patient();
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(
            userId: TestData.UserId
        );
        _referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();
        _referenceData.UserToProfessional[TestData.UserId] = TestData.ProfessionalId;
    }

    private TelemedicineRequest AddRequest(
        AppointmentRequestStatus status = AppointmentRequestStatus.Pending
    )
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
            Reason = "Consulta de control",
            Status = status,
            CreatedBy = TestData.UserId,
        };
        _requests.Items.Add(request);
        return request;
    }

    // --- Aprobación ---

    [Fact]
    public async Task Approve_ProfesionalAsignado_PasaAApprovedSinCita()
    {
        var request = AddRequest();
        var command = new ReviewTelemedicineRequestCommand(
            request.Id,
            RequestDecision.Approved,
            null,
            TestData.UserId,
            HasAdminView: false
        );

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AppointmentRequestStatus.Approved, dto.Status);
        Assert.Equal(AppointmentRequestStatus.Approved, request.Status);
        // El profesional que aprueba su propia solicitud no se auto-notifica.
        Assert.Empty(_alerts.Items);
    }

    [Fact]
    public async Task Approve_Admin_AlertaRequestApprovedAlProfesional()
    {
        var request = AddRequest();
        var command = new ReviewTelemedicineRequestCommand(
            request.Id,
            RequestDecision.Approved,
            null,
            TestData.UserId,
            HasAdminView: true
        );

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AppointmentRequestStatus.Approved, dto.Status);
        var alert = Assert.Single(_alerts.Items);
        Assert.Equal(AlertType.RequestApproved, alert.Type);
        Assert.Equal(TestData.UserId, alert.RecipientUserId);
    }

    [Fact]
    public async Task Approve_SolicitudConvertida_LanzaViolacion()
    {
        var request = AddRequest(AppointmentRequestStatus.Converted);
        var command = new ReviewTelemedicineRequestCommand(
            request.Id,
            RequestDecision.Approved,
            null,
            TestData.UserId,
            HasAdminView: true
        );

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None)
        );
        Assert.Equal(AppointmentRequestStatus.Converted, request.Status);
    }

    [Fact]
    public async Task Approve_UsuarioSinAlcance_LanzaForbidden()
    {
        var request = AddRequest();
        _referenceData.UserToProfessional.Remove(TestData.UserId);
        var command = new ReviewTelemedicineRequestCommand(
            request.Id,
            RequestDecision.Approved,
            null,
            TestData.UserId,
            HasAdminView: false
        );

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _handler.Handle(command, CancellationToken.None)
        );
        Assert.Equal(AppointmentRequestStatus.Pending, request.Status);
    }

    // --- Rechazo ---

    [Fact]
    public async Task Reject_ProfesionalAsignado_PersisteMotivoYEstado()
    {
        var request = AddRequest();
        var command = new ReviewTelemedicineRequestCommand(
            request.Id,
            RequestDecision.Rejected,
            "Sin cupos en el horario",
            TestData.UserId,
            HasAdminView: false
        );

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AppointmentRequestStatus.Rejected, dto.Status);
        Assert.Equal("Sin cupos en el horario", dto.RejectionReason);
        Assert.Equal(AppointmentRequestStatus.Rejected, request.Status);
        Assert.Equal("Sin cupos en el horario", request.RejectionReason);
        var alert = Assert.Single(_alerts.Items);
        Assert.Equal(AlertType.RequestRejected, alert.Type);
    }

    [Fact]
    public async Task Reject_DesdeApproved_EsValido()
    {
        var request = AddRequest(AppointmentRequestStatus.Approved);
        var command = new ReviewTelemedicineRequestCommand(
            request.Id,
            RequestDecision.Rejected,
            "Horario no disponible",
            TestData.UserId,
            HasAdminView: true
        );

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AppointmentRequestStatus.Rejected, dto.Status);
        Assert.Equal("Horario no disponible", dto.RejectionReason);
    }

    [Fact]
    public async Task Reject_SolicitudConvertida_LanzaViolacion()
    {
        var request = AddRequest(AppointmentRequestStatus.Converted);
        var command = new ReviewTelemedicineRequestCommand(
            request.Id,
            RequestDecision.Rejected,
            "Motivo",
            TestData.UserId,
            HasAdminView: true
        );

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Reject_AdminSinPermisoNiAsignacion_LanzaForbidden()
    {
        var request = AddRequest();
        _referenceData.UserToProfessional.Remove(TestData.UserId);
        var command = new ReviewTelemedicineRequestCommand(
            request.Id,
            RequestDecision.Rejected,
            "Motivo",
            TestData.UserId,
            HasAdminView: false
        );

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _handler.Handle(command, CancellationToken.None)
        );
    }
}
