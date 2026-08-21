using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Infrastructure.Repositories;
using CoppAddresd.Telemedicine.UnitTests;

namespace CoppAddresd.Telemedicine.IntegrationTests;

/// <summary>
/// Persistencia del agregado cita contra PostgreSQL real: alta/lectura,
/// historial append-only (cancelación/reprogramación), agenda del profesional y
/// listado admin con filtros y paginación. Cada test usa profesionales únicos
/// (la BD de la colección es compartida) y horarios en UTC explícito.
/// </summary>
[Collection(TelemedicineTestCollection.Name)]
public class AppointmentRepositoryPersistenceTests
{
    private readonly TelemedicineTestContext _ctx;

    public AppointmentRepositoryPersistenceTests(TelemedicineTestDatabase database)
    {
        _ctx = new TelemedicineTestContext(database.ConnectionString);
    }

    /// <summary>Mediodía UTC dentro de <paramref name="days"/> días (sin offset local).</summary>
    private static DateTimeOffset UtcNoon(int days)
        => new(DateTimeOffset.UtcNow.Date.AddDays(days).AddHours(12), TimeSpan.Zero);

    private static TelemedicineAppointment Appointment(
        Guid? professionalId = null, DateTimeOffset? start = null,
        AppointmentStatus status = AppointmentStatus.Confirmed)
        => new()
        {
            ProfessionalId = professionalId ?? Guid.NewGuid(),
            PatientId = TestData.PatientId,
            SpecialtyId = TestData.SpecialtyId,
            OrganizationId = TestData.Org,
            ClinicId = TestData.Clinic,
            LocationId = TestData.LocationId,
            ScheduledStart = start ?? UtcNoon(1),
            ScheduledEnd = (start ?? UtcNoon(1)).AddMinutes(30),
            DurationMinutes = 30,
            Status = status,
            CreatedBy = TestData.UserId,
        };

    [Fact]
    public async Task AltaYLectura_PersistenTodosLosCampos()
    {
        var repo = new AppointmentRepository(_ctx.Create());
        var appointment = Appointment();

        await repo.AddAsync(appointment);

        var loaded = await repo.GetByIdAsync(appointment.Id);
        Assert.NotNull(loaded);
        Assert.Equal(appointment.ProfessionalId, loaded!.ProfessionalId);
        Assert.Equal(appointment.PatientId, loaded.PatientId);
        Assert.Equal(AppointmentStatus.Confirmed, loaded.Status);
        // timestamptz guarda microsegundos: comparar con tolerancia.
        Assert.Equal(
            appointment.ScheduledStart.ToUnixTimeMilliseconds(),
            loaded.ScheduledStart.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task Cancelacion_RegistraHistorialYEstado()
    {
        var repo = new AppointmentRepository(_ctx.Create());
        var appointment = Appointment(status: AppointmentStatus.InProgress);
        await repo.AddAsync(appointment);

        var forUpdate = await repo.GetForUpdateAsync(appointment.Id);
        Assert.NotNull(forUpdate);
        forUpdate!.Status = AppointmentStatus.Cancelled;
        forUpdate.CancellationReason = "Emergencia";
        forUpdate.CancelledBy = CancelledBy.Patient;
        forUpdate.Cancellations.Add(new AppointmentCancellation
        {
            AppointmentId = appointment.Id,
            CancelledBy = CancelledBy.Patient,
            CancelledByUserId = TestData.UserId,
            Reason = "Emergencia",
        });
        await repo.UpdateAsync(forUpdate);

        var loaded = await repo.GetForUpdateAsync(appointment.Id);
        Assert.Equal(AppointmentStatus.Cancelled, loaded!.Status);
        var cancellation = Assert.Single(loaded.Cancellations);
        Assert.Equal("Emergencia", cancellation.Reason);
        Assert.Equal(TestData.UserId, cancellation.CancelledByUserId);
    }

    [Fact]
    public async Task Reprogramacion_RegistraHistorialYContador()
    {
        var repo = new AppointmentRepository(_ctx.Create());
        var appointment = Appointment();
        await repo.AddAsync(appointment);

        var forUpdate = await repo.GetForUpdateAsync(appointment.Id);
        var newStart = appointment.ScheduledStart.AddDays(1);
        forUpdate!.ScheduledStart = newStart;
        forUpdate.ScheduledEnd = newStart.AddMinutes(30);
        forUpdate.RescheduleCount++;
        forUpdate.Reschedules.Add(new AppointmentReschedule
        {
            AppointmentId = appointment.Id,
            RequestedBy = RescheduleRequestedBy.Patient,
            FromStart = appointment.ScheduledStart,
            ToStart = newStart,
        });
        await repo.UpdateAsync(forUpdate);

        var loaded = await repo.GetForUpdateAsync(appointment.Id);
        Assert.Equal(1, loaded!.RescheduleCount);
        var reschedule = Assert.Single(loaded.Reschedules);
        Assert.Equal(appointment.ScheduledStart, reschedule.FromStart);
        Assert.Equal(newStart, reschedule.ToStart);
    }

    [Fact]
    public async Task Agenda_FiltraPorProfesionalYRango()
    {
        var repo = new AppointmentRepository(_ctx.Create());
        var professionalId = Guid.NewGuid();
        // Medianoche UTC (explícito: UtcNow.Date devuelve DateTime Kind=Unspecified).
        var from = new DateTimeOffset(DateTimeOffset.UtcNow.Date.AddDays(2), TimeSpan.Zero);
        await repo.AddAsync(Appointment(professionalId, from.AddHours(10)));
        await repo.AddAsync(Appointment(professionalId, from.AddHours(11)));
        await repo.AddAsync(Appointment(Guid.NewGuid(), from.AddHours(12))); // otro profesional

        var items = await repo.ListByProfessionalAsync(professionalId, from, from.AddDays(1));

        Assert.Equal(2, items.Count);
        Assert.All(items, a => Assert.Equal(professionalId, a.ProfessionalId));
        Assert.Equal(from.AddHours(10), items[0].ScheduledStart);
    }

    [Fact]
    public async Task ListAdmin_FiltrosYPaginacion()
    {
        var repo = new AppointmentRepository(_ctx.Create());
        var clinicId = Guid.NewGuid();
        var status = AppointmentStatus.Completed;
        var from = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);

        for (var i = 0; i < 5; i++)
        {
            var a = Appointment(status: status);
            a.ClinicId = clinicId;
            a.ScheduledStart = from.AddHours(i);
            a.ScheduledEnd = a.ScheduledStart.AddMinutes(30);
            await repo.AddAsync(a);
        }
        await repo.AddAsync(Appointment(status: AppointmentStatus.Confirmed)); // otro estado

        var (items, total) = await repo.ListAdminAsync(
            null, null, clinicId, null, status, from, from.AddDays(1), page: 1, pageSize: 2);

        Assert.Equal(5, total);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task ListAdmin_PaginaFueraDeRango_Vacia()
    {
        var repo = new AppointmentRepository(_ctx.Create());
        var professionalId = Guid.NewGuid();
        await repo.AddAsync(Appointment(professionalId));

        // Filtro por profesional: total determinista aunque la BD compartida
        // acumule citas de otros tests.
        var (items, total) = await repo.ListAdminAsync(
            professionalId, null, null, null, null, null, null, page: 99, pageSize: 20);

        Assert.Equal(1, total);
        Assert.Empty(items);
    }
}