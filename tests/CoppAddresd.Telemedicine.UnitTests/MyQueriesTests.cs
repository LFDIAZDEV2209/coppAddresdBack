using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Pruebas de los queries "mis citas" del profesional (listado y resumen):
/// el alcance por identidad del JWT fuerza que solo se vean los datos del
/// profesional, con el mismo shape que el listado/resumen del admin.
/// </summary>
public class MyQueriesTests
{
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeRequestRepository _requests = new();
    private readonly FakeRoomRepository _rooms = new();
    private readonly FakeAlertRepository _alerts = new();
    private readonly FakeReferenceDataService _referenceData = new();

    public MyQueriesTests()
    {
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional();
        _referenceData.Patients[TestData.PatientId] = TestData.Patient();
        _referenceData.UserToProfessional[TestData.UserId] = TestData.ProfessionalId;
    }

    private static Appointment Appointment(
        AppointmentStatus status,
        DateTimeOffset start,
        Guid? professionalId = null,
        Guid? patientId = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            PatientId = patientId ?? TestData.PatientId,
            ProfessionalId = professionalId ?? TestData.ProfessionalId,
            SpecialtyId = TestData.SpecialtyId,
            OrganizationId = TestData.Org,
            ClinicId = TestData.Clinic,
            LocationId = TestData.LocationId,
            ScheduledStart = start,
            ScheduledEnd = start.AddMinutes(30),
            DurationMinutes = 30,
            Status = status,
            CreatedBy = TestData.UserId,
        };

    private ListMyAppointmentsQueryHandler BuildListHandler() => new(_appointments, _referenceData);

    private GetMySummaryQueryHandler BuildSummaryHandler() =>
        new(_appointments, _requests, _alerts, _rooms);

    private static TelemedicineRequest Request(
        AppointmentRequestStatus status,
        Guid? professionalId = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            PatientId = TestData.PatientId,
            ProfessionalId = professionalId ?? TestData.ProfessionalId,
            SpecialtyId = TestData.SpecialtyId,
            OrganizationId = TestData.Org,
            ClinicId = TestData.Clinic,
            LocationId = TestData.LocationId,
            Status = status,
            CreatedAt = DateTime.UtcNow,
        };

    [Fact]
    public async Task HandleList_SoloDevuelveCitasDelProfesional()
    {
        var otherProfessional = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        _appointments.Items.AddRange([
            Appointment(AppointmentStatus.Confirmed, now.AddDays(1)),
            Appointment(
                AppointmentStatus.Confirmed,
                now.AddDays(2),
                professionalId: otherProfessional
            ),
            Appointment(
                AppointmentStatus.Confirmed,
                now.AddDays(3),
                professionalId: otherProfessional
            ),
        ]);

        var handler = BuildListHandler();
        var result = await handler.Handle(
            new ListMyAppointmentsQuery(
                TestData.ProfessionalId,
                null,
                null,
                null,
                null,
                null,
                1,
                20
            ),
            CancellationToken.None
        );

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal(TestData.ProfessionalId, result.Items[0].ProfessionalId);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task HandleList_AplicaFiltrosDeEstadoYRango()
    {
        var now = DateTimeOffset.UtcNow;
        _appointments.Items.AddRange([
            Appointment(AppointmentStatus.Confirmed, now.AddDays(1)),
            Appointment(AppointmentStatus.Cancelled, now.AddDays(2)),
            Appointment(AppointmentStatus.Confirmed, now.AddDays(30)),
        ]);

        var handler = BuildListHandler();
        var result = await handler.Handle(
            new ListMyAppointmentsQuery(
                TestData.ProfessionalId,
                null,
                null,
                AppointmentStatus.Confirmed,
                now.AddDays(-1),
                now.AddDays(10),
                1,
                20
            ),
            CancellationToken.None
        );

        Assert.Equal(1, result.Total);
        Assert.Equal(AppointmentStatus.Confirmed, result.Items[0].Status);
    }

    [Fact]
    public async Task HandleSummary_AcotaKPIsAlProfesional()
    {
        var otherProfessional = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        _appointments.Items.AddRange([
            Appointment(AppointmentStatus.Confirmed, now),
            Appointment(AppointmentStatus.Confirmed, now.AddHours(1)),
            Appointment(
                AppointmentStatus.Confirmed,
                now.AddHours(2),
                professionalId: otherProfessional
            ),
            Appointment(AppointmentStatus.Completed, now.AddDays(-1)),
        ]);
        _requests.Items.Add(
            new TelemedicineRequest
            {
                Id = Guid.NewGuid(),
                PatientId = TestData.PatientId,
                ProfessionalId = TestData.ProfessionalId,
                SpecialtyId = TestData.SpecialtyId,
                OrganizationId = TestData.Org,
                ClinicId = TestData.Clinic,
                LocationId = TestData.LocationId,
                Status = AppointmentRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            }
        );
        _rooms.Sessions.Add(
            new TelemedicineSession
            {
                Id = Guid.NewGuid(),
                AppointmentId = Guid.NewGuid(),
                Status = TelemedicineSessionStatus.Active,
                CreatedBy = TestData.UserId,
                Appointment = new Appointment
                {
                    Id = Guid.NewGuid(),
                    ProfessionalId = TestData.ProfessionalId,
                },
            }
        );
        _alerts.Items.Add(
            new TelemedicineAlert
            {
                Id = Guid.NewGuid(),
                RecipientUserId = TestData.UserId,
                ReadAt = null,
                CreatedAt = DateTime.UtcNow,
            }
        );

        var handler = BuildSummaryHandler();
        var result = await handler.Handle(
            new GetMySummaryQuery(TestData.ProfessionalId, TestData.UserId),
            CancellationToken.None
        );

        // De hoy: solo las 2 del profesional (la del otro no cuenta).
        Assert.Equal(2, result.AppointmentsToday);
        Assert.Equal(2, result.AppointmentsPending);
        Assert.Equal(1, result.AppointmentsCompleted);
        Assert.Equal(1, result.RequestsPending);
        Assert.Equal(1, result.ActiveSessions);
        Assert.Equal(1, result.AlertsUnread);
    }

    [Fact]
    public async Task HandleListRequests_SoloDevuelveSolicitudesDelProfesional()
    {
        var otherProfessional = Guid.NewGuid();
        _requests.Items.AddRange([
            Request(AppointmentRequestStatus.Pending),
            Request(AppointmentRequestStatus.Pending, professionalId: otherProfessional),
            Request(AppointmentRequestStatus.Pending, professionalId: otherProfessional),
        ]);

        var handler = new ListMyRequestsQueryHandler(_requests, _referenceData);
        var result = await handler.Handle(
            new ListMyRequestsQuery(TestData.ProfessionalId, null, null, null, null, 1, 20),
            CancellationToken.None
        );

        // Solo la solicitud del profesional autenticado (las del otro no cuentan).
        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal(TestData.ProfessionalId, result.Items[0].ProfessionalId);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task HandleListRequests_AplicaFiltroDeEstado()
    {
        _requests.Items.AddRange([
            Request(AppointmentRequestStatus.Pending),
            Request(AppointmentRequestStatus.Converted),
            Request(AppointmentRequestStatus.Rejected),
        ]);

        var handler = new ListMyRequestsQueryHandler(_requests, _referenceData);
        var result = await handler.Handle(
            new ListMyRequestsQuery(
                TestData.ProfessionalId,
                AppointmentRequestStatus.Converted,
                null,
                null,
                null,
                1,
                20
            ),
            CancellationToken.None
        );

        Assert.Equal(1, result.Total);
        Assert.Equal(AppointmentRequestStatus.Converted, result.Items[0].Status);
    }
}
