using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.UnitTests;

public class GetDashboardAnalyticsQueryHandlerTests
{
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeReferenceDataService _referenceData = new();

    private GetDashboardAnalyticsQueryHandler BuildHandler()
    {
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional();
        _referenceData.Patients[TestData.PatientId] = TestData.Patient();
        return new GetDashboardAnalyticsQueryHandler(_appointments, _referenceData);
    }

    private static Appointment Appointment(
        AppointmentStatus status,
        DateTimeOffset start,
        Guid? professionalId = null,
        Guid? patientId = null)
        => new()
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

    [Fact]
    public async Task Handle_VistaGlobal_CalculaKPIsYSeriesCompletas()
    {
        var now = DateTimeOffset.UtcNow;
        _appointments.Items.AddRange(
        [
            Appointment(AppointmentStatus.Completed, now.AddDays(-3)),
            Appointment(AppointmentStatus.Completed, now.AddDays(-2)),
            Appointment(AppointmentStatus.Cancelled, now.AddDays(-1)),
            Appointment(AppointmentStatus.Confirmed, now.AddHours(-2)),
            Appointment(AppointmentStatus.NoShow, now.AddDays(-10)),
        ]);

        var handler = BuildHandler();
        var result = await handler.Handle(new GetDashboardAnalyticsQuery(null, now.AddDays(-30), now), CancellationToken.None);

        Assert.Equal(5, result.Kpis.TotalAppointments);
        Assert.Equal(2, result.Kpis.Completed);
        Assert.Equal(1, result.Kpis.Cancelled);
        Assert.Equal(1, result.Kpis.NoShow);
        Assert.Equal(1, result.Kpis.Pending);
        Assert.Equal(1, result.Kpis.UniquePatients);
        Assert.Equal(1, result.Kpis.ActiveProfessionals);

        // Serie continua: un punto por cada día del rango (31 días), con huecos en 0.
        Assert.Equal(31, result.DailySeries.Count);
        Assert.All(result.DailySeries, d => Assert.InRange(d.Count, 0, 5));

        // La distribución por estado solo incluye estados presentes.
        Assert.Equal(4, result.StatusDistribution.Count);
        Assert.Contains(result.StatusDistribution, s => s.Status == AppointmentStatus.Completed && s.Count == 2);
    }

    [Fact]
    public async Task Handle_VistaGlobal_IncluyeActividadPorProfesionalConNombreResuelto()
    {
        var now = DateTimeOffset.UtcNow;
        var otherProfessional = Guid.NewGuid();
        _appointments.Items.AddRange(
        [
            Appointment(AppointmentStatus.Completed, now.AddDays(-5)),
            Appointment(AppointmentStatus.Completed, now.AddDays(-4), professionalId: otherProfessional),
            Appointment(AppointmentStatus.Completed, now.AddDays(-4), professionalId: otherProfessional),
            Appointment(AppointmentStatus.Cancelled, now.AddDays(-3), professionalId: otherProfessional),
        ]);

        var handler = BuildHandler();
        var result = await handler.Handle(new GetDashboardAnalyticsQuery(null, now.AddDays(-30), now), CancellationToken.None);

        Assert.Equal(2, result.ProfessionalActivity.Count);

        var other = result.ProfessionalActivity.First(a => a.ProfessionalId == otherProfessional);
        Assert.Equal(3, other.Total);
        Assert.Equal(2, other.Completed);
        Assert.Equal(1, other.Cancelled);

        var mine = result.ProfessionalActivity.First(a => a.ProfessionalId == TestData.ProfessionalId);
        Assert.Equal(1, mine.Total);
        Assert.Equal("Dra. Ana Pérez", mine.ProfessionalName);
    }

    [Fact]
    public async Task Handle_Profesional_SoloVeSusDatosSinActividadGlobal()
    {
        var now = DateTimeOffset.UtcNow;
        var otherProfessional = Guid.NewGuid();
        _appointments.Items.AddRange(
        [
            Appointment(AppointmentStatus.Completed, now.AddDays(-5)),
            Appointment(AppointmentStatus.Cancelled, now.AddDays(-4), professionalId: otherProfessional),
            Appointment(AppointmentStatus.Completed, now.AddDays(-3), professionalId: otherProfessional),
        ]);

        var handler = BuildHandler();
        var result = await handler.Handle(
            new GetDashboardAnalyticsQuery(TestData.ProfessionalId, now.AddDays(-30), now),
            CancellationToken.None);

        Assert.Equal(1, result.Kpis.TotalAppointments);
        Assert.Equal(1, result.Kpis.Completed);
        Assert.Empty(result.ProfessionalActivity);
        Assert.Equal(0, result.Kpis.ActiveProfessionals);
    }

    [Fact]
    public async Task Handle_SeriesPorHora_SiempreCubreLas24Horas()
    {
        var now = DateTimeOffset.UtcNow;
        _appointments.Items.AddRange(
        [
            Appointment(AppointmentStatus.Completed, new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero)),
            Appointment(AppointmentStatus.Completed, new DateTimeOffset(2026, 8, 10, 9, 30, 0, TimeSpan.Zero)),
            Appointment(AppointmentStatus.Completed, new DateTimeOffset(2026, 8, 11, 14, 0, 0, TimeSpan.Zero)),
        ]);

        var handler = BuildHandler();
        var result = await handler.Handle(
            new GetDashboardAnalyticsQuery(null, new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero)),
            CancellationToken.None);

        Assert.Equal(24, result.HourlyDistribution.Count);
        Assert.Equal(2, result.HourlyDistribution.First(h => h.Hour == 9).Count);
        Assert.Equal(1, result.HourlyDistribution.First(h => h.Hour == 14).Count);
        Assert.Equal(Enumerable.Range(0, 24), result.HourlyDistribution.Select(h => h.Hour));
    }

    [Fact]
    public async Task Handle_ProximasCitas_ResuelveNombresYSoloFuturas()
    {
        var now = DateTimeOffset.UtcNow;
        _appointments.Items.AddRange(
        [
            Appointment(AppointmentStatus.Completed, now.AddDays(-1)),
            Appointment(AppointmentStatus.Confirmed, now.AddDays(1)),
            Appointment(AppointmentStatus.Confirmed, now.AddDays(3)),
            Appointment(AppointmentStatus.Cancelled, now.AddDays(5)),
        ]);

        var handler = BuildHandler();
        var result = await handler.Handle(new GetDashboardAnalyticsQuery(null, now.AddDays(-30), now.AddDays(30)), CancellationToken.None);

        Assert.Equal(3, result.UpcomingAppointments.Count);
        Assert.All(result.UpcomingAppointments, a => Assert.True(a.ScheduledStart >= now));
        Assert.Equal("María Gómez", result.UpcomingAppointments[0].PatientName);
        Assert.DoesNotContain(result.UpcomingAppointments, a => a.Status == AppointmentStatus.Completed);
    }
}