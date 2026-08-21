using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using CoppAddresd.Telemedicine.Infrastructure.Repositories;
using CoppAddresd.Telemedicine.UnitTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.IntegrationTests;

/// <summary>
/// Idempotencia y atomicidad del webhook del proveedor contra PostgreSQL real:
/// el índice único de la clave de evento serializa los duplicados y la
/// transacción del webhook garantiza todo-o-nada (el duplicado NO muta estado).
/// Cada test siembra su propia cita (profesional único) sobre la BD compartida.
/// </summary>
[Collection(TelemedicineTestCollection.Name)]
public class WebhookIdempotencyTests
{
    private readonly TelemedicineTestContext _ctx;

    public WebhookIdempotencyTests(TelemedicineTestDatabase database)
    {
        _ctx = new TelemedicineTestContext(database.ConnectionString);
    }

    /// <summary>Procesador de webhooks con repositorios y transacción REALES sobre un contexto compartido.</summary>
    private async Task<(ProcessTwilioWebhookCommandHandler Handler, Guid AppointmentId, string RoomSid)> CreateHandlerWithAppointmentAsync(
        bool startSession = true)
    {
        var db = _ctx.Create();
        var appointment = new TelemedicineAppointment
        {
            ProfessionalId = Guid.NewGuid(), // único por test (BD compartida)
            PatientId = TestData.PatientId,
            SpecialtyId = TestData.SpecialtyId,
            OrganizationId = TestData.Org,
            ClinicId = TestData.Clinic,
            LocationId = TestData.LocationId,
            ScheduledStart = DateTimeOffset.UtcNow.AddMinutes(5),
            ScheduledEnd = DateTimeOffset.UtcNow.AddMinutes(35),
            DurationMinutes = 30,
            Status = AppointmentStatus.InProgress,
            CreatedBy = TestData.UserId,
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var roomSid = $"RM-{Guid.NewGuid():N}";
        var room = new VirtualRoom
        {
            AppointmentId = appointment.Id,
            Provider = "twilio",
            ProviderRoomName = $"apt-{appointment.Id:N}",
            ProviderRoomSid = roomSid,
        };
        if (startSession)
        {
            room.Sessions.Add(new TelemedicineSession
            {
                AppointmentId = appointment.Id,
                Status = TelemedicineSessionStatus.Active,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                CreatedBy = TestData.UserId,
            });
        }
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var referenceData = new FakeReferenceDataService();
        referenceData.Professionals[appointment.ProfessionalId] =
            TestData.Professional(id: appointment.ProfessionalId, userId: TestData.UserId);
        referenceData.Patients[TestData.PatientId] = TestData.Patient();
        referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();

        var handler = new ProcessTwilioWebhookCommandHandler(
            new FakeVideoProvider { SignatureValid = true },
            new RoomRepository(db),
            new AppointmentRepository(db),
            referenceData,
            new AlertRepository(db),
            new TelemedicineUnitOfWork(db),
            NullLogger<ProcessTwilioWebhookCommandHandler>.Instance);

        return (handler, appointment.Id, roomSid);
    }

    private static ProcessTwilioWebhookCommand RoomEndedCommand(string roomSid)
        => new(
            "https://x/api/v1/telemedicine/webhooks/twilio",
            "sig",
            new Dictionary<string, string>
            {
                ["EventType"] = "room-ended",
                ["RoomSid"] = roomSid,
                ["ParticipantSid"] = "",
            });

    [Fact]
    public async Task RoomEnded_CompletaCitaYPersisteTodo()
    {
        var (handler, appointmentId, roomSid) = await CreateHandlerWithAppointmentAsync();

        var result = await handler.Handle(RoomEndedCommand(roomSid), CancellationToken.None);

        Assert.Equal(WebhookProcessOutcome.Processed, result.Outcome);

        await using var db = _ctx.Create();
        var appointment = await db.Appointments
            .Include(a => a.Room!).ThenInclude(r => r.Sessions)
            .SingleAsync(a => a.Id == appointmentId);
        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        Assert.Equal(VirtualRoomStatus.Ended, appointment.Room!.Status);
        var session = Assert.Single(appointment.Room.Sessions);
        Assert.Equal(TelemedicineSessionStatus.Ended, session.Status);
        Assert.Equal("room-ended", session.EndReason);
        Assert.Equal(1, await db.WebhookEvents.CountAsync(e => e.RoomSid == roomSid));
        Assert.Equal(1, await db.Alerts.CountAsync(a => a.RelatedAppointmentId == appointmentId));
    }

    [Fact]
    public async Task WebhookDuplicado_DevuelveDuplicateYSinDobleMutacion()
    {
        var (handler, appointmentId, roomSid) = await CreateHandlerWithAppointmentAsync();
        var command = RoomEndedCommand(roomSid);

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(WebhookProcessOutcome.Processed, first.Outcome);
        Assert.Equal(WebhookProcessOutcome.Duplicate, second.Outcome);

        await using var db = _ctx.Create();
        Assert.Equal(1, await db.WebhookEvents.CountAsync(e => e.RoomSid == roomSid));
        // Una sola alerta de sesión finalizada (no duplicada por el reintento).
        Assert.Equal(1, await db.Alerts.CountAsync(a => a.RelatedAppointmentId == appointmentId));
    }

    [Fact]
    public async Task WebhookSalaDesconocida_RegistraSinMutaciones()
    {
        var (handler, appointmentId, roomSid) = await CreateHandlerWithAppointmentAsync();
        var unknownSid = $"RM-desconocida-{Guid.NewGuid():N}";

        var result = await handler.Handle(RoomEndedCommand(unknownSid), CancellationToken.None);

        Assert.Equal(WebhookProcessOutcome.Processed, result.Outcome);
        await using var db = _ctx.Create();
        Assert.Equal(1, await db.WebhookEvents.CountAsync(e => e.RoomSid == unknownSid));
        Assert.Equal(0, await db.Alerts.CountAsync(a => a.RelatedAppointmentId == appointmentId));
    }
}