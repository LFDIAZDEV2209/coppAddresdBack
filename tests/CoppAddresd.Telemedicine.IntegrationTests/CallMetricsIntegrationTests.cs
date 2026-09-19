using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Infrastructure.Metrics;
using CoppAddresd.Telemedicine.Infrastructure.Repositories;
using CoppAddresd.Telemedicine.UnitTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.IntegrationTests;

/// <summary>
/// F5 contra PostgreSQL real: el backfill reconstruye las claves de llamada
/// (salas, sesiones, duración sumada, reaperturas y chat por rol) con su espejo
/// global, y el lector <c>GetCallMetricsAsync</c> devuelve el mismo agregado por
/// el fast path del rollup y por el fallback vivo sin rollup.
/// </summary>
[Collection(TelemedicineTestCollection.Name)]
public class CallMetricsIntegrationTests
{
    private readonly TelemedicineTestContext _ctx;

    public CallMetricsIntegrationTests(TelemedicineTestDatabase database)
    {
        _ctx = new TelemedicineTestContext(database.ConnectionString);
    }

    private static DateOnly RecentDay() =>
        DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-3));

    private static DateTimeOffset UtcAt(DateOnly day, int hour) =>
        new(day.ToDateTime(TimeOnly.MinValue).AddHours(hour), TimeSpan.Zero);

    /// <summary>
    /// Siembra una consulta con sala, una sesión terminada de 1800 s, una
    /// reapertura y dos mensajes de chat (Professional/Patient) en la clínica
    /// indicada, y devuelve el profesional y el día de agenda.
    /// </summary>
    private async Task<(Guid ProfessionalId, DateOnly Day)> SeedCallAsync(Guid clinic)
    {
        var professionalId = Guid.NewGuid();
        var day = RecentDay();
        var start = UtcAt(day, 10);

        await using var db = _ctx.Create();
        var appointment = new Appointment
        {
            ProfessionalId = professionalId,
            PatientId = TestData.PatientId,
            SpecialtyId = TestData.SpecialtyId,
            OrganizationId = TestData.Org,
            ClinicId = clinic,
            LocationId = TestData.LocationId,
            ScheduledStart = start,
            ScheduledEnd = start.AddMinutes(30),
            DurationMinutes = 30,
            Status = AppointmentStatus.Completed,
            ReopenCount = 1,
            CompletedAt = start.AddMinutes(35),
            CreatedBy = TestData.UserId,
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var room = new VirtualRoom
        {
            AppointmentId = appointment.Id,
            Provider = "twilio",
            ProviderRoomSid = $"RM-{appointment.Id:N}",
            ProviderRoomName = $"apt-{appointment.Id:N}",
            Status = VirtualRoomStatus.Ended,
            ScheduledOpenAt = start.AddMinutes(-10),
            ScheduledCloseAt = start.AddMinutes(45),
            CreatedBy = TestData.UserId,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        db.Sessions.Add(new TelemedicineSession
        {
            AppointmentId = appointment.Id,
            RoomId = room.Id,
            Status = TelemedicineSessionStatus.Ended,
            StartedAt = start,
            EndedAt = start.AddMinutes(30),
            DurationSeconds = 1800,
            CreatedBy = TestData.UserId,
        });
        db.ChatMessages.Add(new ChatMessage
        {
            AppointmentId = appointment.Id,
            SenderUserId = TestData.UserId,
            SenderRole = "Professional",
            Body = "hola",
            CreatedAt = start.UtcDateTime,
        });
        db.ChatMessages.Add(new ChatMessage
        {
            AppointmentId = appointment.Id,
            SenderUserId = TestData.PatientUserId,
            SenderRole = "Patient",
            Body = "hola",
            CreatedAt = start.UtcDateTime,
        });
        await db.SaveChangesAsync();

        return (professionalId, day);
    }

    private async Task<Dictionary<string, long>> RollupTotalsAsync(
        Guid clinic,
        Guid professionalId
    )
    {
        await using var db = _ctx.Create();
        var rows = await db
            .AppointmentDailyMetrics.AsNoTracking()
            .Where(m => m.ClinicId == clinic && m.ProfessionalId == professionalId)
            .ToListAsync();

        return rows.ToDictionary(
            m => $"{m.MetricKey}|{m.DimensionKey}",
            m => m.TotalCount,
            StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task Backfill_ReconstruyeClavesDeLlamadaYChatConEspejoGlobal()
    {
        var clinic = Guid.NewGuid();
        var (professionalId, day) = await SeedCallAsync(clinic);

        await new MetricsBackfillService(_ctx.Create(), NullLogger<MetricsBackfillService>.Instance)
            .BackfillAsync(day, day, clinic, dryRun: false, CancellationToken.None);

        var totals = await RollupTotalsAsync(clinic, professionalId);
        Assert.Equal(1, totals["rooms_opened|general"]);
        Assert.Equal(1, totals["sessions_started|general"]);
        Assert.Equal(1, totals["sessions_ended|general"]);
        Assert.Equal(1800, totals["session_duration_seconds|general"]);
        Assert.Equal(1, totals["reopens|general"]);
        Assert.Equal(1, totals["chat_messages_sent|Professional"]);
        Assert.Equal(1, totals["chat_messages_sent|Patient"]);

        // Espejo global (Guid.Empty): mismas claves sumadas.
        var global = await RollupTotalsAsync(clinic, Guid.Empty);
        Assert.Equal(1, global["rooms_opened|general"]);
        Assert.Equal(1, global["sessions_ended|general"]);
        Assert.Equal(1800, global["session_duration_seconds|general"]);
        Assert.Equal(2, global["chat_messages_sent|Professional"] + global["chat_messages_sent|Patient"]);
    }

    [Fact]
    public async Task GetCallMetrics_SinRollup_UsaConteosVivos()
    {
        var clinic = Guid.NewGuid();
        var (professionalId, day) = await SeedCallAsync(clinic);
        var from = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var metrics = await new AppointmentRepository(_ctx.Create())
            .GetCallMetricsAsync(professionalId, from, from.AddDays(1), CancellationToken.None);

        Assert.Equal(1, metrics.RoomsOpened);
        Assert.Equal(1, metrics.SessionsStarted);
        Assert.Equal(1, metrics.SessionsEnded);
        Assert.Equal(1800, metrics.TotalDurationSeconds);
        Assert.Equal(1, metrics.Reopens);
        Assert.Equal(1, metrics.ChatMessagesByRole["Professional"]);
        Assert.Equal(1, metrics.ChatMessagesByRole["Patient"]);
        Assert.Equal(0, metrics.JoinTokensIssued);
    }

    [Fact]
    public async Task GetCallMetrics_ConRollup_UsaLasFilasPreAgregadas()
    {
        var clinic = Guid.NewGuid();
        var (professionalId, day) = await SeedCallAsync(clinic);

        await new MetricsBackfillService(_ctx.Create(), NullLogger<MetricsBackfillService>.Instance)
            .BackfillAsync(day, day, clinic, dryRun: false, CancellationToken.None);

        var from = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var metrics = await new AppointmentRepository(_ctx.Create())
            .GetCallMetricsAsync(professionalId, from, from.AddDays(1), CancellationToken.None);

        Assert.Equal(1, metrics.RoomsOpened);
        Assert.Equal(1, metrics.SessionsStarted);
        Assert.Equal(1, metrics.SessionsEnded);
        Assert.Equal(1800, metrics.TotalDurationSeconds);
        Assert.Equal(1, metrics.Reopens);
        Assert.Equal(1, metrics.ChatMessagesByRole["Professional"]);
        Assert.Equal(1, metrics.ChatMessagesByRole["Patient"]);
    }
}
