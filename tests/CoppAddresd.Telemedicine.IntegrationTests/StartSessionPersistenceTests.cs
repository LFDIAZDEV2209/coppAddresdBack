using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using CoppAddresd.Telemedicine.Infrastructure.Repositories;
using CoppAddresd.Telemedicine.UnitTests;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Telemedicine.IntegrationTests;

/// <summary>
/// Persistencia del flujo de inicio de sesión: cita Confirmed + sala → se agrega
/// una sesión activa al agregado y se persiste (INSERT) junto con la transición
/// de estado de la cita. Reproduce el camino exacto de StartSessionCommandHandler.
/// </summary>
[Collection(TelemedicineTestCollection.Name)]
public class StartSessionPersistenceTests
{
    private readonly TelemedicineTestContext _ctx;

    public StartSessionPersistenceTests(TelemedicineTestDatabase database)
    {
        _ctx = new TelemedicineTestContext(database.ConnectionString);
    }

    [Fact]
    public async Task AgregarSesionAlAgregado_PersisteComoInsert()
    {
        // Seed: cita Confirmed + sala (como la crea el join-token).
        var appointment = new Appointment
        {
            ProfessionalId = Guid.NewGuid(),
            PatientId = TestData.PatientId,
            SpecialtyId = TestData.SpecialtyId,
            OrganizationId = TestData.Org,
            ClinicId = TestData.Clinic,
            LocationId = TestData.LocationId,
            ScheduledStart = DateTimeOffset.UtcNow.AddMinutes(5),
            ScheduledEnd = DateTimeOffset.UtcNow.AddMinutes(35),
            DurationMinutes = 30,
            Status = AppointmentStatus.Confirmed,
            CreatedBy = TestData.UserId,
        };
        var room = new VirtualRoom
        {
            AppointmentId = appointment.Id,
            Provider = "twilio",
            ProviderRoomName = $"apt-{appointment.Id:N}",
            ProviderRoomSid = "RM-flow-test",
        };
        await using (var seed = _ctx.Create())
        {
            seed.Appointments.Add(appointment);
            seed.Rooms.Add(room);
            await seed.SaveChangesAsync();
        }

        // Flujo del handler: cargar agregado → agregar sesión → UpdateAsync.
        var repo = new AppointmentRepository(_ctx.Create());
        var loaded = await repo.GetForUpdateAsync(appointment.Id);
        Assert.NotNull(loaded);

        loaded!.Sessions.Add(new TelemedicineSession
        {
            AppointmentId = appointment.Id,
            RoomId = room.Id,
            Status = TelemedicineSessionStatus.Active,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedBy = TestData.UserId,
        });
        loaded.Status = AppointmentStatus.InProgress;
        loaded.UpdatedAt = DateTimeOffset.UtcNow.UtcDateTime;

        await repo.UpdateAsync(loaded);

        // Verificar en BD: una sesión INSERTada y la cita InProgress.
        await using var verify = _ctx.Create();
        var session = await verify.Sessions.SingleAsync(s => s.AppointmentId == appointment.Id);
        Assert.Equal(TelemedicineSessionStatus.Active, session.Status);
        Assert.Equal(room.Id, session.RoomId);
        var state = await verify.Appointments.AsNoTracking()
            .SingleAsync(a => a.Id == appointment.Id);
        Assert.Equal(AppointmentStatus.InProgress, state.Status);
    }
}