using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Alcance dual de los handlers con el paciente de la app mÃ³vil (identidad del
/// JWT, sin permisos ERP): creaciÃ³n de solicitudes, "mis solicitudes",
/// "mis citas" y cancelaciÃ³n como paciente. Cierra el IDOR de
/// <c>requests/mine</c> y garantiza que el paciente solo opera sobre su perfil.
/// </summary>
public class PatientScopeTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeRequestRepository _requests = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly FakeRoomRepository _rooms = new();
    private readonly FakeAlertRepository _alerts = new();

    public PatientScopeTests()
    {
        _referenceData.Patients[TestData.PatientId] = TestData.Patient();
        _referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(
            userId: TestData.UserId
        );
        _referenceData.Locations[TestData.LocationId] = TestData.Location();
        // El usuario del JWT es el paciente (vinculaciÃ³n app.patient_profiles.user_id).
        _referenceData.UserToPatient[TestData.PatientUserId] = TestData.PatientId;
    }

    // â”€â”€ CreaciÃ³n de solicitudes (POST /requests) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Create_PacientePropio_CreaSolicitudPending()
    {
        var handler = new CreateTelemedicineRequestCommandHandler(
            _requests,
            _referenceData,
            _settings,
            _alerts
        );

        var dto = await handler.Handle(
            new CreateTelemedicineRequestCommand(
                TestData.PatientId,
                TestData.Org,
                TestData.SpecialtyId,
                TestData.ProfessionalId,
                TestData.Clinic,
                TestData.LocationId,
                null,
                "Dolor de cabeza",
                TestData.PatientUserId,
                ErpMode: false
            ),
            CancellationToken.None
        );

        Assert.Equal(AppointmentRequestStatus.Pending, dto.Status);
        Assert.Equal(TestData.PatientId, dto.PatientId);
        // Alerta NewRequest al profesional elegido (bandeja del ERP).
        var alert = Assert.Single(_alerts.Items);
        Assert.Equal(AlertType.NewRequest, alert.Type);
    }

    [Fact]
    public async Task Create_PacienteAjeno_LanzaForbidden()
    {
        var handler = new CreateTelemedicineRequestCommandHandler(
            _requests,
            _referenceData,
            _settings,
            _alerts
        );

        var otherPatient = Guid.NewGuid();
        _referenceData.Patients[otherPatient] = TestData.Patient(id: otherPatient);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(
                new CreateTelemedicineRequestCommand(
                    otherPatient,
                    TestData.Org,
                    TestData.SpecialtyId,
                    null,
                    TestData.Clinic,
                    TestData.LocationId,
                    null,
                    "Dolor",
                    TestData.PatientUserId,
                    ErpMode: false
                ),
                CancellationToken.None
            )
        );

        Assert.Empty(_requests.Items);
    }

    [Fact]
    public async Task Create_SinPerfilPaciente_LanzaForbidden()
    {
        var handler = new CreateTelemedicineRequestCommandHandler(
            _requests,
            _referenceData,
            _settings,
            _alerts
        );

        // Usuario ERP sin perfil de paciente (sin UserToPatient): 403.
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(
                new CreateTelemedicineRequestCommand(
                    TestData.PatientId,
                    TestData.Org,
                    TestData.SpecialtyId,
                    null,
                    TestData.Clinic,
                    TestData.LocationId,
                    null,
                    "Dolor",
                    TestData.UserId,
                    ErpMode: false
                ),
                CancellationToken.None
            )
        );

        Assert.Empty(_requests.Items);
    }

    // â”€â”€ Mis solicitudes (GET /requests/mine) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Mine_PacienteSinParam_DevuelveSoloLasSuyas()
    {
        _requests.Items.Add(
            new TelemedicineRequest
            {
                Id = Guid.NewGuid(),
                PatientId = TestData.PatientId,
                OrganizationId = TestData.Org,
                SpecialtyId = TestData.SpecialtyId,
                Status = AppointmentRequestStatus.Pending,
                Reason = "Consulta",
                CreatedBy = TestData.PatientUserId,
            }
        );
        _requests.Items.Add(
            new TelemedicineRequest
            {
                Id = Guid.NewGuid(),
                PatientId = Guid.NewGuid(),
                OrganizationId = TestData.Org,
                SpecialtyId = TestData.SpecialtyId,
                Status = AppointmentRequestStatus.Pending,
                Reason = "De otro paciente",
                CreatedBy = Guid.NewGuid(),
            }
        );

        var handler = new GetMyRequestsQueryHandler(_requests, _referenceData);
        var result = await handler.Handle(
            new GetMyRequestsQuery(null, TestData.PatientUserId, ErpMode: false),
            CancellationToken.None
        );

        var request = Assert.Single(result);
        Assert.Equal(TestData.PatientId, request.PatientId);
    }

    [Fact]
    public async Task Mine_PacienteConPatientIdAjeno_LanzaForbidden()
    {
        var handler = new GetMyRequestsQueryHandler(_requests, _referenceData);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(
                new GetMyRequestsQuery(Guid.NewGuid(), TestData.PatientUserId, ErpMode: false),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task Mine_ErpConPatientId_ConservaComportamiento()
    {
        var targetPatient = Guid.NewGuid();
        _requests.Items.Add(
            new TelemedicineRequest
            {
                Id = Guid.NewGuid(),
                PatientId = targetPatient,
                OrganizationId = TestData.Org,
                SpecialtyId = TestData.SpecialtyId,
                Status = AppointmentRequestStatus.Pending,
                Reason = "Consulta ERP",
                CreatedBy = TestData.UserId,
            }
        );

        var handler = new GetMyRequestsQueryHandler(_requests, _referenceData);
        var result = await handler.Handle(
            new GetMyRequestsQuery(targetPatient, TestData.UserId, ErpMode: true),
            CancellationToken.None
        );

        var request = Assert.Single(result);
        Assert.Equal(targetPatient, request.PatientId);
    }

    // â”€â”€ Mis citas (GET /appointments/mine) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task MyAppointments_Paciente_ListaSoloSusCitas()
    {
        _appointments.Items.Add(TestData.Appointment(patientId: TestData.PatientId));
        _appointments.Items.Add(TestData.Appointment(patientId: Guid.NewGuid()));

        var handler = new GetMyAppointmentsQueryHandler(
            _appointments,
            _referenceData,
            _settings,
            _rooms
        );
        var result = await handler.Handle(
            new GetMyAppointmentsQuery(
                TestData.PatientUserId,
                null,
                null,
                null,
                Page: 1,
                PageSize: 20
            ),
            CancellationToken.None
        );

        var dto = Assert.Single(result.Items);
        Assert.Equal(TestData.PatientId, dto.PatientId);
    }

    [Fact]
    public async Task MyAppointments_PacienteFiltraPorEstado()
    {
        _appointments.Items.Add(
            TestData.Appointment(status: AppointmentStatus.Confirmed, patientId: TestData.PatientId)
        );
        _appointments.Items.Add(
            TestData.Appointment(status: AppointmentStatus.Completed, patientId: TestData.PatientId)
        );

        var handler = new GetMyAppointmentsQueryHandler(
            _appointments,
            _referenceData,
            _settings,
            _rooms
        );
        var result = await handler.Handle(
            new GetMyAppointmentsQuery(
                TestData.PatientUserId,
                AppointmentStatus.Confirmed,
                null,
                null,
                1,
                20
            ),
            CancellationToken.None
        );

        var dto = Assert.Single(result.Items);
        Assert.Equal(AppointmentStatus.Confirmed, dto.Status);
    }

    [Fact]
    public async Task MyAppointments_Paciente_IncluyeVentanaDesdeSettings()
    {
        var start = DateTimeOffset.UtcNow.AddHours(5);
        var appointment = TestData.Appointment(patientId: TestData.PatientId, start: start);
        _appointments.Items.Add(appointment);
        _settings.Settings.RoomOpenBeforeMinutes = 5;
        _settings.Settings.RoomCloseAfterMinutes = 20;

        var handler = new GetMyAppointmentsQueryHandler(
            _appointments,
            _referenceData,
            _settings,
            _rooms
        );
        var result = await handler.Handle(
            new GetMyAppointmentsQuery(TestData.PatientUserId, null, null, null, 1, 20),
            CancellationToken.None
        );

        var dto = Assert.Single(result.Items);
        Assert.Equal(start.AddMinutes(-5), dto.RoomOpensAt);
        Assert.Equal(appointment.ScheduledEnd.AddMinutes(20), dto.RoomClosesAt);
    }

    [Fact]
    public async Task MyAppointments_CitaReabierta_CorrigeVentanaDesdeReopenedAt()
    {
        var start = DateTimeOffset.UtcNow.AddHours(5);
        var appointment = TestData.Appointment(patientId: TestData.PatientId, start: start);
        appointment.ReopenedAt = start.AddMinutes(45);
        _appointments.Items.Add(appointment);

        var handler = new GetMyAppointmentsQueryHandler(
            _appointments,
            _referenceData,
            _settings,
            _rooms
        );
        var result = await handler.Handle(
            new GetMyAppointmentsQuery(TestData.PatientUserId, null, null, null, 1, 20),
            CancellationToken.None
        );

        var dto = Assert.Single(result.Items);
        // La ventana corre desde la reapertura y cierra con el mayor de los fines.
        var reopenedEnd = appointment.ReopenedAt.Value.AddMinutes(appointment.DurationMinutes);
        Assert.Equal(appointment.ReopenedAt.Value.AddMinutes(-10), dto.RoomOpensAt);
        Assert.Equal(reopenedEnd.AddMinutes(15), dto.RoomClosesAt);
    }

    [Fact]
    public async Task MyAppointments_SalaPersistida_PrefiereSuVentana()
    {
        var appointment = TestData.Appointment(patientId: TestData.PatientId);
        _appointments.Items.Add(appointment);
        var open = appointment.ScheduledStart.AddMinutes(-60);
        var close = appointment.ScheduledEnd.AddMinutes(120);
        _rooms.Rooms.Add(
            new VirtualRoom
            {
                AppointmentId = appointment.Id,
                ProviderRoomSid = "RM-test",
                ProviderRoomName = $"apt-{appointment.Id:N}",
                ScheduledOpenAt = open,
                ScheduledCloseAt = close,
                CreatedBy = TestData.UserId,
            }
        );

        var handler = new GetMyAppointmentsQueryHandler(
            _appointments,
            _referenceData,
            _settings,
            _rooms
        );
        var result = await handler.Handle(
            new GetMyAppointmentsQuery(TestData.PatientUserId, null, null, null, 1, 20),
            CancellationToken.None
        );

        var dto = Assert.Single(result.Items);
        Assert.Equal(open, dto.RoomOpensAt);
        Assert.Equal(close, dto.RoomClosesAt);
        // La sala persistida sigue siendo la autoridad para la ventana; los
        // settings se resuelven una vez por contexto para exponer la gracia F5.
        Assert.Equal(1, _settings.Calls);
        Assert.Equal(_settings.Settings.ReopenGraceMinutes, dto.ReopenGraceMinutes);
    }

    [Fact]
    public async Task MyAppointments_SinPerfilPaciente_LanzaForbidden()
    {
        var handler = new GetMyAppointmentsQueryHandler(
            _appointments,
            _referenceData,
            _settings,
            _rooms
        );

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(
                new GetMyAppointmentsQuery(TestData.UserId, null, null, null, 1, 20),
                CancellationToken.None
            )
        );
    }

    // â”€â”€ CancelaciÃ³n como paciente (POST /appointments/{id}/cancel) â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Cancel_PacienteDeLaCita_CancelaConHistorial()
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.Confirmed);
        _appointments.Items.Add(appointment);

        var handler = new CancelAppointmentCommandHandler(_appointments, _referenceData, _alerts);
        var dto = await handler.Handle(
            new CancelAppointmentCommand(
                appointment.Id,
                "No puedo asistir",
                CancelledBy.Professional,
                TestData.PatientUserId,
                PatientUserId: TestData.PatientUserId
            ),
            CancellationToken.None
        );

        Assert.Equal(AppointmentStatus.Cancelled, dto.Status);
        var cancellation = Assert.Single(appointment.Cancellations);
        Assert.Equal(CancelledBy.Patient, cancellation.CancelledBy);
        Assert.Equal(TestData.PatientUserId, cancellation.CancelledByUserId);
        var alert = Assert.Single(_alerts.Items);
        Assert.Equal(AlertType.AppointmentCancelled, alert.Type);
    }

    [Fact]
    public async Task Cancel_PacienteCitaAjena_LanzaForbidden()
    {
        var appointment = TestData.Appointment(
            status: AppointmentStatus.Confirmed,
            patientId: Guid.NewGuid()
        );
        _appointments.Items.Add(appointment);

        var handler = new CancelAppointmentCommandHandler(_appointments, _referenceData, _alerts);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(
                new CancelAppointmentCommand(
                    appointment.Id,
                    "RazÃ³n",
                    CancelledBy.Patient,
                    TestData.PatientUserId,
                    PatientUserId: TestData.PatientUserId
                ),
                CancellationToken.None
            )
        );
        Assert.Empty(appointment.Cancellations);
    }

    [Fact]
    public async Task Cancel_PacienteCitaEnCurso_LanzaViolacion()
    {
        var appointment = TestData.Appointment(
            status: AppointmentStatus.InProgress,
            patientId: TestData.PatientId
        );
        _appointments.Items.Add(appointment);

        var handler = new CancelAppointmentCommandHandler(_appointments, _referenceData, _alerts);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            handler.Handle(
                new CancelAppointmentCommand(
                    appointment.Id,
                    "RazÃ³n",
                    CancelledBy.Patient,
                    TestData.PatientUserId,
                    PatientUserId: TestData.PatientUserId
                ),
                CancellationToken.None
            )
        );
        Assert.Equal(AppointmentStatus.InProgress, appointment.Status);
    }

    [Fact]
    public async Task Cancel_PacienteSinPerfilResoluble_LanzaForbidden()
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.Confirmed);
        _appointments.Items.Add(appointment);

        var handler = new CancelAppointmentCommandHandler(_appointments, _referenceData, _alerts);

        // Usuario sin UserToPatient: no es paciente â†’ 403.
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(
                new CancelAppointmentCommand(
                    appointment.Id,
                    "RazÃ³n",
                    CancelledBy.Patient,
                    TestData.UserId,
                    PatientUserId: TestData.UserId
                ),
                CancellationToken.None
            )
        );
    }
}
