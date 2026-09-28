using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using CoppAddresd.Telemedicine.Infrastructure.Repositories;
using CoppAddresd.Telemedicine.UnitTests;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Telemedicine.IntegrationTests;

/// <summary>
/// Garantía real de integridad del calendario (anti doble reserva) contra
/// PostgreSQL: la exclusión GiST y el índice único parcial deben impedir dos
/// citas activas que se solapen para el mismo profesional, incluso con dos
/// inserciones concurrentes en contextos distintos.
/// </summary>
[Collection(TelemedicineTestCollection.Name)]
public class AppointmentConcurrencyTests
{
    private readonly TelemedicineTestContext _ctx;

    public AppointmentConcurrencyTests(TelemedicineTestDatabase database)
    {
        _ctx = new TelemedicineTestContext(database.ConnectionString);
    }

    private static Appointment ActiveAppointment(
        Guid professionalId,
        DateTimeOffset start,
        int duration = 30
    ) =>
        new()
        {
            ProfessionalId = professionalId,
            PatientId = TestData.PatientId,
            SpecialtyId = TestData.SpecialtyId,
            OrganizationId = TestData.Org,
            ClinicId = TestData.Clinic,
            LocationId = TestData.LocationId,
            ScheduledStart = start,
            ScheduledEnd = start.AddMinutes(duration),
            DurationMinutes = duration,
            Status = AppointmentStatus.Confirmed,
            CreatedBy = TestData.UserId,
        };

    private static async Task<(bool Ok, Exception? Error)> TryAddAsync(
        IAppointmentRepository repo,
        Appointment appointment
    )
    {
        try
        {
            await repo.AddAsync(appointment);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex);
        }
    }

    private static async Task<(bool Ok, Exception? Error)> TryUpdateAsync(
        IAppointmentRepository repo,
        Appointment appointment
    )
    {
        try
        {
            await repo.UpdateAsync(appointment);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex);
        }
    }

    [Fact]
    public async Task DosInsercionesConcurrentesSolapadas_UnaFallaConExclusion()
    {
        var professionalId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var a1 = ActiveAppointment(professionalId, start);
        var a2 = ActiveAppointment(professionalId, start.AddMinutes(15));

        var repo1 = new AppointmentRepository(_ctx.Create());
        var repo2 = new AppointmentRepository(_ctx.Create());

        var results = await Task.WhenAll(TryAddAsync(repo1, a1), TryAddAsync(repo2, a2));

        var failures = results.Where(r => !r.Ok).ToList();
        Assert.Single(failures);
        Assert.IsType<BusinessRuleViolationException>(failures[0].Error);

        // Solo una cita quedó persistida.
        await using var verify = _ctx.Create();
        var count = await verify.Appointments.CountAsync(a => a.ProfessionalId == professionalId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MismoInicioExacto_ViolaIndiceUnicoParcial()
    {
        var professionalId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var a1 = ActiveAppointment(professionalId, start);
        var a2 = ActiveAppointment(professionalId, start);

        var (_, firstError) = await TryAddAsync(new AppointmentRepository(_ctx.Create()), a1);
        Assert.Null(firstError);

        var (secondOk, secondError) = await TryAddAsync(
            new AppointmentRepository(_ctx.Create()),
            a2
        );
        Assert.False(secondOk);
        Assert.IsType<BusinessRuleViolationException>(secondError);
    }

    [Fact]
    public async Task CitasNoSolapadas_AmbasPersisten()
    {
        var professionalId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var a1 = ActiveAppointment(professionalId, start, duration: 30);
        var a2 = ActiveAppointment(professionalId, start.AddMinutes(30), duration: 30);

        var (ok1, e1) = await TryAddAsync(new AppointmentRepository(_ctx.Create()), a1);
        var (ok2, e2) = await TryAddAsync(new AppointmentRepository(_ctx.Create()), a2);

        Assert.True(ok1, e1?.ToString());
        Assert.True(ok2, e2?.ToString());
    }

    [Fact]
    public async Task CitaCancelada_NoBloqueaElCalendario()
    {
        var professionalId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var a1 = ActiveAppointment(professionalId, start);
        a1.Status = AppointmentStatus.Cancelled; // fuera del predicado de la exclusión
        var (ok1, e1) = await TryAddAsync(new AppointmentRepository(_ctx.Create()), a1);
        Assert.True(ok1, e1?.ToString());

        var a2 = ActiveAppointment(professionalId, start); // se solapa pero la otra está cancelada
        var (ok2, e2) = await TryAddAsync(new AppointmentRepository(_ctx.Create()), a2);
        Assert.True(ok2, e2?.ToString());
    }

    /// <summary>
    /// Carrera de reprogramación (citas-e2e 1.6): dos citas confirmadas en
    /// slots distintos se mueven al mismo slot libre desde contextos
    /// separados. La exclusión GiST deja pasar a una y rechaza a la otra con
    /// <c>BusinessRuleViolationException</c> (el chequeo de aplicación
    /// <c>HasActiveOverlapAsync</c> no ve la transacción rival).
    /// </summary>
    [Fact]
    public async Task ReprogramacionConcurrenteAlMismoSlot_UnaFallaConExclusion()
    {
        var professionalId = Guid.NewGuid();
        var target = DateTimeOffset.UtcNow.AddDays(1);

        var a1 = ActiveAppointment(professionalId, target.AddHours(-3));
        var a2 = ActiveAppointment(professionalId, target.AddHours(3));
        var (ok1, e1) = await TryAddAsync(new AppointmentRepository(_ctx.Create()), a1);
        Assert.True(ok1, e1?.ToString());
        var (ok2, e2) = await TryAddAsync(new AppointmentRepository(_ctx.Create()), a2);
        Assert.True(ok2, e2?.ToString());

        await using var ctx1 = _ctx.Create();
        await using var ctx2 = _ctx.Create();
        var repo1 = new AppointmentRepository(ctx1);
        var repo2 = new AppointmentRepository(ctx2);
        var m1 = await repo1.GetForUpdateAsync(a1.Id);
        var m2 = await repo2.GetForUpdateAsync(a2.Id);
        Assert.NotNull(m1);
        Assert.NotNull(m2);
        m1!.ScheduledStart = target;
        m1.ScheduledEnd = target.AddMinutes(30);
        m2!.ScheduledStart = target;
        m2.ScheduledEnd = target.AddMinutes(30);

        var results = await Task.WhenAll(TryUpdateAsync(repo1, m1), TryUpdateAsync(repo2, m2));

        var failures = results.Where(r => !r.Ok).ToList();
        Assert.Single(failures);
        Assert.IsType<BusinessRuleViolationException>(failures[0].Error);

        // Solo una de las dos ocupa el slot disputado.
        await using var verify = _ctx.Create();
        var occupying = await verify.Appointments.CountAsync(a =>
            a.ProfessionalId == professionalId
            && a.Status == AppointmentStatus.Confirmed
            && a.ScheduledStart < target.AddMinutes(30)
            && a.ScheduledEnd > target
        );
        Assert.Equal(1, occupying);
    }

    [Fact]
    public async Task UnaSolicitudUnaCita_IndiceUnicoRequestId()
    {
        var requestId = Guid.NewGuid();
        // La FK cita → solicitud exige que la solicitud exista primero.
        await using (var seed = _ctx.Create())
        {
            seed.Requests.Add(
                new TelemedicineRequest
                {
                    Id = requestId,
                    PatientId = TestData.PatientId,
                    OrganizationId = TestData.Org,
                    SpecialtyId = TestData.SpecialtyId,
                    Reason = "Dolor abdominal",
                    Status = AppointmentRequestStatus.Pending,
                    CreatedBy = TestData.UserId,
                }
            );
            await seed.SaveChangesAsync();
        }

        var start = DateTimeOffset.UtcNow.AddDays(1);
        var a1 = ActiveAppointment(Guid.NewGuid(), start);
        a1.RequestId = requestId;
        var a2 = ActiveAppointment(Guid.NewGuid(), start.AddMinutes(40));
        a2.RequestId = requestId;

        var (ok1, e1) = await TryAddAsync(new AppointmentRepository(_ctx.Create()), a1);
        Assert.True(ok1, e1?.ToString());

        var (ok2, e2) = await TryAddAsync(new AppointmentRepository(_ctx.Create()), a2);
        Assert.False(ok2);
        Assert.IsType<BusinessRuleViolationException>(e2);
    }
}
