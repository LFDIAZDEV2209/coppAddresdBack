using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Caso de uso de creación de solicitud (CreateTelemedicineRequestCommandHandler):
/// validación de referencias, especialidad compatible con el profesional elegido,
/// ventana de fecha preferida y materialización de la alerta NewRequest.
/// </summary>
public class CreateRequestHandlerTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeRequestRepository _requests = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly FakeAlertRepository _alerts = new();
    private readonly FakeTelemedicineNotifier _notifier = new();
    private readonly CreateTelemedicineRequestCommandHandler _handler;

    public CreateRequestHandlerTests()
    {
        _handler = new CreateTelemedicineRequestCommandHandler(
            _requests,
            _referenceData,
            _settings,
            _alerts,
            notifier: _notifier
        );
        _referenceData.Patients[TestData.PatientId] = TestData.Patient();
        _referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();
    }

    [Fact]
    public async Task Handle_Valido_CreaSolicitudPending()
    {
        var command = new CreateTelemedicineRequestCommand(
            TestData.PatientId,
            TestData.Org,
            TestData.SpecialtyId,
            null,
            TestData.Clinic,
            TestData.LocationId,
            null,
            "Dolor abdominal",
            TestData.UserId,
            ErpMode: true
        );

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AppointmentRequestStatus.Pending, dto.Status);
        var entity = Assert.Single(_requests.Items);
        Assert.Equal(TestData.PatientId, entity.PatientId);
        Assert.Null(entity.ProfessionalId);
        // Sin profesional elegido → no hay destinatario → sin alerta ni notificación.
        Assert.Empty(_alerts.Items);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task Handle_ConProfesional_ValidaEspecialidadYEmiteAlerta()
    {
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(
            userId: TestData.UserId
        );

        var command = new CreateTelemedicineRequestCommand(
            TestData.PatientId,
            TestData.Org,
            TestData.SpecialtyId,
            TestData.ProfessionalId,
            TestData.Clinic,
            TestData.LocationId,
            null,
            "Dolor abdominal",
            TestData.UserId,
            ErpMode: true
        );

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(TestData.ProfessionalId, dto.ProfessionalId);
        var alert = Assert.Single(_alerts.Items);
        Assert.Equal(AlertType.NewRequest, alert.Type);
        Assert.Equal(TestData.UserId, alert.RecipientUserId);
    }

    [Fact]
    public async Task Handle_ConProfesional_EnviaPushDeNuevaSolicitud()
    {
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(
            userId: TestData.UserId
        );

        var command = new CreateTelemedicineRequestCommand(
            TestData.PatientId,
            TestData.Org,
            TestData.SpecialtyId,
            TestData.ProfessionalId,
            TestData.Clinic,
            TestData.LocationId,
            null,
            "Dolor abdominal",
            TestData.UserId,
            ErpMode: true
        );

        await _handler.Handle(command, CancellationToken.None);

        var notification = Assert.Single(_notifier.Sent);
        Assert.Equal(TestData.UserId, notification.UserId);
        Assert.Equal("Nueva solicitud de María Gómez", notification.Title);
        Assert.Equal(
            new[] { TelemedicineNotificationChannel.Push },
            notification.Channels
        );
    }

    [Fact]
    public async Task Handle_ProfesionalNoAtiendeEspecialidad_LanzaViolacion()
    {
        var otherSpecialty = Guid.NewGuid();
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(
            userId: TestData.UserId,
            specialtyIds: [otherSpecialty]
        );

        var command = new CreateTelemedicineRequestCommand(
            TestData.PatientId,
            TestData.Org,
            TestData.SpecialtyId,
            TestData.ProfessionalId,
            TestData.Clinic,
            TestData.LocationId,
            null,
            "Dolor abdominal",
            TestData.UserId,
            ErpMode: true
        );

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None)
        );
        Assert.Empty(_requests.Items);
    }

    [Fact]
    public async Task Handle_ProfesionalSinEspecialidades_SaltaValidacion()
    {
        // Profesional sin catálogo de especialidades: no se bloquea la solicitud.
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(
            userId: TestData.UserId,
            specialtyIds: []
        );

        var command = new CreateTelemedicineRequestCommand(
            TestData.PatientId,
            TestData.Org,
            TestData.SpecialtyId,
            TestData.ProfessionalId,
            TestData.Clinic,
            TestData.LocationId,
            null,
            "Dolor abdominal",
            TestData.UserId,
            ErpMode: true
        );

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(TestData.ProfessionalId, dto.ProfessionalId);
    }

    [Fact]
    public async Task Handle_FechaPreferidaMuyCercana_LanzaViolacion()
    {
        var command = new CreateTelemedicineRequestCommand(
            TestData.PatientId,
            TestData.Org,
            TestData.SpecialtyId,
            null,
            TestData.Clinic,
            TestData.LocationId,
            DateTimeOffset.UtcNow.AddHours(_settings.Settings.MinAdvanceBookingHours - 1),
            "Dolor abdominal",
            TestData.UserId,
            ErpMode: true
        );

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None)
        );
        Assert.Empty(_requests.Items);
    }

    [Fact]
    public async Task Handle_FechaPreferidaMuyLejana_LanzaViolacion()
    {
        var command = new CreateTelemedicineRequestCommand(
            TestData.PatientId,
            TestData.Org,
            TestData.SpecialtyId,
            null,
            TestData.Clinic,
            TestData.LocationId,
            DateTimeOffset.UtcNow.AddDays(_settings.Settings.MaxAdvanceBookingDays + 1),
            "Dolor abdominal",
            TestData.UserId,
            ErpMode: true
        );

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Handle_PacienteInexistente_LanzaNotFound()
    {
        var command = new CreateTelemedicineRequestCommand(
            Guid.NewGuid(),
            TestData.Org,
            TestData.SpecialtyId,
            null,
            TestData.Clinic,
            TestData.LocationId,
            null,
            "Dolor abdominal",
            TestData.UserId,
            ErpMode: true
        );

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None)
        );
    }
}
