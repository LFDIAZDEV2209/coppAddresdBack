using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Infrastructure.Repositories;
using CoppAddresd.Telemedicine.UnitTests;

namespace CoppAddresd.Telemedicine.IntegrationTests;

/// <summary>
/// Ciclo del paciente de la app móvil contra PostgreSQL real: crear solicitud
/// (identidad paciente) → confirmar como profesional → "mis citas" lista la
/// cita → cancelar como paciente (CancelledBy.Patient) con historial y alerta
/// persistidos. Usa repositorios reales y el handler de cada paso.
/// </summary>
[Collection(TelemedicineTestCollection.Name)]
public class PatientAppointmentCycleTests
{
    private readonly TelemedicineTestContext _ctx;

    public PatientAppointmentCycleTests(TelemedicineTestDatabase database)
    {
        _ctx = new TelemedicineTestContext(database.ConnectionString);
    }

    /// <summary>Mediodía UTC dentro de <paramref name="days"/> días (sin offset local).</summary>
    private static DateTimeOffset UtcNoon(int days) =>
        new(DateTimeOffset.UtcNow.Date.AddDays(days).AddHours(12), TimeSpan.Zero);

    [Fact]
    public async Task CicloCompleto_CrearConfirmarListarYCancelar()
    {
        // Identidades únicas por corrida (BD compartida de la colección).
        var patientId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();
        var patientUserId = Guid.NewGuid();

        var referenceData = new FakeReferenceDataService();
        referenceData.Patients[patientId] = TestData.Patient(id: patientId);
        referenceData.Professionals[professionalId] = TestData.Professional(
            id: professionalId,
            userId: Guid.NewGuid()
        );
        referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();
        referenceData.Locations[TestData.LocationId] = TestData.Location();
        // El usuario del JWT es el paciente (vínculo app.patient_profiles.user_id).
        referenceData.UserToPatient[patientUserId] = patientId;

        var requestsRepo = new RequestRepository(_ctx.Create());
        var appointmentsRepo = new AppointmentRepository(_ctx.Create());
        var roomsRepo = new RoomRepository(_ctx.Create());
        var alertsRepo = new AlertRepository(_ctx.Create());
        var settings = new FakeSettingsProvider();

        // 1) El paciente crea la solicitud (alcance por identidad).
        var createHandler = new CreateTelemedicineRequestCommandHandler(
            requestsRepo,
            referenceData,
            settings,
            alertsRepo
        );
        var requestDto = await createHandler.Handle(
            new CreateTelemedicineRequestCommand(
                patientId,
                TestData.Org,
                TestData.SpecialtyId,
                professionalId,
                TestData.Clinic,
                TestData.LocationId,
                UtcNoon(1),
                "Control",
                patientUserId,
                ErpMode: false
            ),
            CancellationToken.None
        );

        Assert.Equal(AppointmentRequestStatus.Pending, requestDto.Status);

        // 2) El profesional confirma → cita Confirmed + solicitud Converted.
        var confirmHandler = new ConfirmTelemedicineRequestCommandHandler(
            requestsRepo,
            appointmentsRepo,
            referenceData,
            settings,
            alertsRepo
        );
        var appointmentDto = await confirmHandler.Handle(
            new ConfirmTelemedicineRequestCommand(
                requestDto.Id,
                professionalId,
                UtcNoon(1),
                30,
                TestData.LocationId,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        Assert.Equal(AppointmentStatus.Confirmed, appointmentDto.Status);

        // 3) "Mis citas" del paciente lista la cita (identidad, filtro por estado).
        var myAppointments = new GetMyAppointmentsQueryHandler(
            appointmentsRepo,
            referenceData,
            settings,
            roomsRepo
        );
        var page = await myAppointments.Handle(
            new GetMyAppointmentsQuery(
                patientUserId,
                AppointmentStatus.Confirmed,
                null,
                null,
                1,
                20
            ),
            CancellationToken.None
        );

        var mine = Assert.Single(page.Items);
        Assert.Equal(appointmentDto.Id, mine.Id);
        Assert.Equal(patientId, mine.PatientId);

        // 4) El paciente cancela su cita → Cancelled + historial + alerta persistidos.
        var cancelHandler = new CancelAppointmentCommandHandler(
            appointmentsRepo,
            referenceData,
            alertsRepo
        );
        var cancelled = await cancelHandler.Handle(
            new CancelAppointmentCommand(
                appointmentDto.Id,
                "No puedo asistir",
                CancelledBy.Professional,
                patientUserId,
                PatientUserId: patientUserId
            ),
            CancellationToken.None
        );

        Assert.Equal(AppointmentStatus.Cancelled, cancelled.Status);

        // Historial append-only persistido.
        var reloaded = await appointmentsRepo.GetForUpdateAsync(appointmentDto.Id);
        var cancellation = Assert.Single(reloaded!.Cancellations);
        Assert.Equal(CancelledBy.Patient, cancellation.CancelledBy);
        Assert.Equal(patientUserId, cancellation.CancelledByUserId);

        // Alerta AppointmentCancelled al profesional persistida.
        var alerts = await alertsRepo.ListForUserAsync(
            referenceData.Professionals[professionalId].UserId!.Value,
            false,
            1,
            20,
            CancellationToken.None
        );
        Assert.Contains(alerts.Items, a => a.Type == AlertType.AppointmentCancelled);
    }

    [Fact]
    public async Task Cancel_PacienteCitaAjena_NoCambiaEstado()
    {
        var patientId = Guid.NewGuid();
        var patientUserId = Guid.NewGuid();
        var otherPatientId = Guid.NewGuid();

        var referenceData = new FakeReferenceDataService();
        referenceData.Patients[patientId] = TestData.Patient(id: patientId);
        referenceData.Patients[otherPatientId] = TestData.Patient(id: otherPatientId);
        referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(
            userId: TestData.UserId
        );
        referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();
        referenceData.UserToPatient[patientUserId] = patientId;

        var appointmentsRepo = new AppointmentRepository(_ctx.Create());
        var alertsRepo = new AlertRepository(_ctx.Create());

        // Cita de OTRO paciente (mismo profesional, horario único).
        var appointment = new Appointment
        {
            PatientId = otherPatientId,
            ProfessionalId = TestData.ProfessionalId,
            SpecialtyId = TestData.SpecialtyId,
            OrganizationId = TestData.Org,
            ClinicId = TestData.Clinic,
            LocationId = TestData.LocationId,
            ScheduledStart = UtcNoon(2),
            ScheduledEnd = UtcNoon(2).AddMinutes(30),
            DurationMinutes = 30,
            Status = AppointmentStatus.Confirmed,
            CreatedBy = TestData.UserId,
        };
        await appointmentsRepo.AddAsync(appointment);

        var cancelHandler = new CancelAppointmentCommandHandler(
            appointmentsRepo,
            referenceData,
            alertsRepo
        );

        await Assert.ThrowsAsync<CoppAddresd.Telemedicine.Domain.Exceptions.ForbiddenException>(
            () =>
                cancelHandler.Handle(
                    new CancelAppointmentCommand(
                        appointment.Id,
                        "Razón",
                        CancelledBy.Patient,
                        patientUserId,
                        PatientUserId: patientUserId
                    ),
                    CancellationToken.None
                )
        );

        var reloaded = await appointmentsRepo.GetByIdAsync(appointment.Id);
        Assert.Equal(AppointmentStatus.Confirmed, reloaded!.Status);
        Assert.Empty(reloaded.Cancellations);
    }
}
