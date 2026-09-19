using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// F5: exposición de la gracia de reapertura efectiva en el detalle de la cita
/// (ERP) y en el listado del paciente (contrato homogéneo), con el mismo
/// patrón aditivo de <c>RoomOpensAt</c>/<c>RoomClosesAt</c>.
/// </summary>
public class ReopenGraceExposureTests
{
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly FakeRoomRepository _rooms = new();

    public ReopenGraceExposureTests()
    {
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(
            userId: TestData.UserId);
        _referenceData.Patients[TestData.PatientId] = TestData.Patient();
        _referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();
        _referenceData.UserToPatient[TestData.PatientUserId] = TestData.PatientId;
        _settings.Settings.ReopenGraceMinutes = 30;
    }

    [Fact]
    public async Task GetAppointment_ExponeLaGraciaEfectivaDelSettings()
    {
        var appointment = TestData.Appointment();
        _appointments.Items.Add(appointment);
        var handler = new GetAppointmentQueryHandler(_appointments, _referenceData, _settings);

        var dto = await handler.Handle(
            new GetAppointmentQuery(appointment.Id), CancellationToken.None);

        Assert.Equal(30, dto.ReopenGraceMinutes);
        Assert.NotNull(dto.RoomOpensAt);
    }

    [Fact]
    public async Task GetMyAppointments_ExponeLaGraciaEnItemsSinSala()
    {
        var appointment = TestData.Appointment(patientId: TestData.PatientId);
        _appointments.Items.Add(appointment);
        var handler = new GetMyAppointmentsQueryHandler(
            _appointments, _referenceData, _settings, _rooms);

        var result = await handler.Handle(
            new GetMyAppointmentsQuery(TestData.PatientUserId, null, null, null, 1, 20),
            CancellationToken.None);

        Assert.Equal(30, Assert.Single(result.Items).ReopenGraceMinutes);
    }

    [Fact]
    public async Task GetMyAppointments_ExponeLaGraciaTambienConSalaPersistida()
    {
        var appointment = TestData.Appointment(patientId: TestData.PatientId);
        _appointments.Items.Add(appointment);
        _rooms.Rooms.Add(new VirtualRoom
        {
            AppointmentId = appointment.Id,
            ProviderRoomSid = "RM-test",
            ProviderRoomName = $"apt-{appointment.Id:N}",
            ScheduledOpenAt = appointment.ScheduledStart.AddMinutes(-60),
            ScheduledCloseAt = appointment.ScheduledEnd.AddMinutes(120),
            CreatedBy = TestData.UserId,
        });
        var handler = new GetMyAppointmentsQueryHandler(
            _appointments, _referenceData, _settings, _rooms);

        var result = await handler.Handle(
            new GetMyAppointmentsQuery(TestData.PatientUserId, null, null, null, 1, 20),
            CancellationToken.None);

        Assert.Equal(30, Assert.Single(result.Items).ReopenGraceMinutes);
    }
}
