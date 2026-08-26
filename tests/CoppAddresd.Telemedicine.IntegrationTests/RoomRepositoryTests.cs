using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Infrastructure.Repositories;

using CoppAddresd.Telemedicine.UnitTests;

namespace CoppAddresd.Telemedicine.IntegrationTests;

/// <summary>
/// Idempotencia de la sala virtual contra PostgreSQL: la sala es 1:1 con la cita
/// y su nombre es único por proveedor — una creación duplicada (carrera de dos
/// join-token) devuelve la existente.
/// </summary>
[Collection(TelemedicineTestCollection.Name)]
public class RoomRepositoryTests
{
    private readonly TelemedicineTestContext _ctx;

    public RoomRepositoryTests(TelemedicineTestDatabase database)
    {
        _ctx = new TelemedicineTestContext(database.ConnectionString);
    }

    private static async Task<Appointment> SeedAppointmentAsync(TelemedicineTestContext ctx)
    {
        var appointment = new Appointment
        {
            // Profesional único por test (la BD de la colección es compartida).
            ProfessionalId = Guid.NewGuid(),
            PatientId = TestData.PatientId,
            SpecialtyId = TestData.SpecialtyId,
            OrganizationId = TestData.Org,
            ClinicId = TestData.Clinic,
            LocationId = TestData.LocationId,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddMinutes(30),
            DurationMinutes = 30,
            Status = AppointmentStatus.Confirmed,
            CreatedBy = TestData.UserId,
        };
        await using var db = ctx.Create();
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment;
    }

    [Fact]
    public async Task MismaCita_LaSegundaCreacionDevuelveLaExistente()
    {
        var appointment = await SeedAppointmentAsync(_ctx);
        var repo = new RoomRepository(_ctx.Create());
        var first = new VirtualRoom
        {
            AppointmentId = appointment.Id,
            Provider = "twilio",
            ProviderRoomName = $"apt-{appointment.Id:N}",
            ProviderRoomSid = "RM1",
        };

        await repo.AddAsync(first);

        // Reintento de otro join-token concurrente (misma cita, mismo nombre):
        // devuelve la sala existente, sin violación.
        var retry = new VirtualRoom
        {
            AppointmentId = appointment.Id,
            Provider = "twilio",
            ProviderRoomName = $"apt-{appointment.Id:N}",
            ProviderRoomSid = "RM2",
        };
        var returned = await repo.AddAsync(retry);

        Assert.Equal(first.Id, returned.Id);
        Assert.Equal("RM1", returned.ProviderRoomSid);
    }

    [Fact]
    public async Task WebhookEvent_Duplicado_LanzaBusinessRule()
    {
        var repo = new RoomRepository(_ctx.Create());
        var first = new TelemedicineWebhookEvent
        {
            EventType = "room-ended",
            RoomSid = "RM123",
            ParticipantSid = "PS1",
            PayloadJson = "{}",
        };
        await repo.AddWebhookEventAsync(first);

        var duplicate = new TelemedicineWebhookEvent
        {
            EventType = "room-ended",
            RoomSid = "RM123",
            ParticipantSid = "PS1",
            PayloadJson = "{}",
        };
        await Assert.ThrowsAsync<CoppAddresd.Telemedicine.Domain.Exceptions.BusinessRuleViolationException>(
            () => repo.AddWebhookEventAsync(duplicate));
    }

    [Fact]
    public async Task WebhookEvent_DistintoParticipante_NoEsDuplicado()
    {
        var repo = new RoomRepository(_ctx.Create());
        await repo.AddWebhookEventAsync(new TelemedicineWebhookEvent
        {
            EventType = "participant-connected",
            RoomSid = "RM123",
            ParticipantSid = "PS1",
            PayloadJson = "{}",
        });

        // El mismo evento para otro participante es una fila distinta.
        await repo.AddWebhookEventAsync(new TelemedicineWebhookEvent
        {
            EventType = "participant-connected",
            RoomSid = "RM123",
            ParticipantSid = "PS2",
            PayloadJson = "{}",
        });
    }
}