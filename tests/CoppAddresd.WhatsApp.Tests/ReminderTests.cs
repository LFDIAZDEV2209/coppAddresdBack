using System.Text.Json;
using CoppAddresd.Application.Features.ProgramProgress.Commands.BulkAssignProgramContent;
using CoppAddresd.Application.Features.ProgramProgress.Commands.CompleteTask;
using CoppAddresd.Application.Features.HealthTests.Execution;
using CoppAddresd.Application.Features.HealthTests.Scoring;
using CoppAddresd.Application.Features.ProgramProgress.Queries;
using CoppAddresd.Application.WhatsApp.Messaging;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Entities.WhatsApp;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Repositories;
using CoppAddresd.WhatsApp.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.WhatsApp.Tests;

public sealed class ManualClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-11-12T15:00:00Z");
    public override DateTimeOffset GetUtcNow() => Now;
}

[Trait("Category", "Unit")]
public sealed class ScheduleTests
{
    [Theory]
    [InlineData("2026-11-12T12:00:00Z", "2026-11-12T14:00:00Z")]
    [InlineData("2026-11-12T22:59:00Z", "2026-11-12T22:59:00Z")]
    [InlineData("2026-11-12T23:00:00Z", "2026-11-13T14:00:00Z")]
    [InlineData("2026-11-13T23:00:00Z", "2026-11-16T14:00:00Z")]
    [InlineData("2026-11-14T16:00:00Z", "2026-11-16T14:00:00Z")]
    public void Window_RespectsBogotaAndBusinessDay(string input, string expected) =>
        Assert.Equal(DateTimeOffset.Parse(expected), ReminderSchedule.InWindow(DateTimeOffset.Parse(input), new()));

    [Fact]
    public void Daily_NeverRepeatsBefore24Hours()
    {
        var now = DateTimeOffset.Parse("2026-11-12T15:00:00Z");
        var last = now.AddHours(-20);
        Assert.True(ReminderSchedule.Daily(now, last, new()) >= last.AddHours(24));
    }

    [Theory]
    [InlineData(1, 120)] [InlineData(2, 240)] [InlineData(3, 480)] [InlineData(4, 960)] [InlineData(5, 1920)]
    public void Backoff_Uses120MinutesAndExponentialGrowth(int attempt, int minutes) =>
        Assert.Equal(TimeSpan.FromMinutes(minutes), ReminderSchedule.Backoff(attempt));
}

[Trait("Category", "Integration")]
public sealed class ReminderTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly ManualClock clock = new();
    private readonly ReminderSettings settings = new() { Enabled = true };
    public async Task InitializeAsync()
    {
        await fixture.CreateAppSchemaAsync(CancellationToken.None);
        await using var db = fixture.CreateDb();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM whatsapp.reminder_deliveries; DELETE FROM whatsapp.reminders; DELETE FROM whatsapp.raw_events; DELETE FROM whatsapp.conversations; DELETE FROM whatsapp.patient_links;", CancellationToken.None);
    }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RealSources_CreateFourKinds_AndCancelEachResolvedReason()
    {
        var source = await SeedAsync();
        await using var app = fixture.CreateAppDb();
        await using var db = fixture.CreateDb();
        var query = new ProgramAccompanimentQuery(app);
        var snapshot = await query.GetSnapshotAsync([source.PatientId], clock.Now, CancellationToken.None);
        Assert.Equal(3, snapshot.Reasons.Count);
        var scheduler = new WhatsAppReminderScheduler(db, query, settings, clock);
        await scheduler.ScheduleBatchAsync(null, CancellationToken.None);
        Assert.Equal(4, await db.Reminders.CountAsync());
        Assert.All(await db.Reminders.ToListAsync(), r => Assert.NotEmpty(r.Detail));
        // Fuentes canónicas reales: racha calificada hoy, podcast completado, evaluación entregada.
        await app.StreakStates.Where(x => x.EnrollmentId == source.EnrollmentId)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.LastActiveDate, source.Today), CancellationToken.None);
        var checkin = new DailyCheckIn { Id = Guid.NewGuid(), EnrollmentId = source.EnrollmentId,
            ProgramWeekId = source.WeekId, LocalDate = source.Today, Weekday = 4 };
        app.DailyCheckIns.Add(checkin);
        await app.SaveChangesAsync();
        app.TaskCompletions.Add(new TaskCompletion { Id = Guid.NewGuid(), EnrollmentId = source.EnrollmentId,
            ProgramWeekId = source.WeekId, DailyCheckinId = checkin.Id, LocalDate = source.Today,
            Weekday = 4, TaskCode = TaskCode.podcast, SourceRefType = "auto_media", MediaId = source.MediaId });
        await app.HealthTestAssignments.Where(x => x.Id == source.AssignmentId).ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, HealthTestAssignmentStatus.completed));
        await app.SaveChangesAsync();
        await scheduler.ScheduleBatchAsync(null, CancellationToken.None);
        db.ChangeTracker.Clear();
        var reminders = await db.Reminders.ToListAsync();
        Assert.All(reminders.Where(r => r.Kind != ReminderKind.checkin), r => Assert.Equal(ReminderState.Cancelled, r.State));
        Assert.Equal(ReminderState.Pending, reminders.Single(r => r.Kind == ReminderKind.checkin).State);
    }

    [Fact]
    public async Task RealEvaluationSubmit_CancelsReminderAfterClinicalCommit()
    {
        var source = await SeedAsync();
        await using var app = fixture.CreateAppDb();
        await using var db = fixture.CreateDb();
        var query = new ProgramAccompanimentQuery(app);
        var scheduler = new WhatsAppReminderScheduler(db, query, settings, clock);
        await scheduler.ScheduleBatchAsync(null, CancellationToken.None);
        var hooks = new WhatsAppReminderHooks(db, scheduler, query);
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IScoreStrategy, SumScoreStrategy>(HealthTestScoringStrategy.sum);
        using var provider = services.BuildServiceProvider();
        var handler = new SubmitEvaluationCommandHandler(new HealthTestRepository(app), new ScoreStrategyRegistry(provider),
            new ScoreRangeEngine(), new IndicatorEngine(), new AlertEngine(), whatsappReminders: hooks);
        var result = await handler.Handle(new SubmitEvaluationCommand(source.AssignmentId, new SubmitEvaluationRequest([])), CancellationToken.None);
        Assert.Equal(HealthTestEvaluationStatus.completed, result.Status);
        Assert.Equal(ReminderState.Cancelled, (await db.Reminders.AsNoTracking().SingleAsync(r => r.AssignmentId == source.AssignmentId)).State);
        Assert.Equal(HealthTestAssignmentStatus.completed, (await app.HealthTestAssignments.AsNoTracking().SingleAsync(x => x.Id == source.AssignmentId)).Status);
    }

    [Fact]
    public async Task RealProgramTaskCompletion_ResolvesPodcastAndStreakReminders()
    {
        var source = await SeedAsync();
        await using var app = fixture.CreateAppDb();
        await using var db = fixture.CreateDb();
        var query = new ProgramAccompanimentQuery(app);
        var scheduler = new WhatsAppReminderScheduler(db, query, settings, clock);
        await scheduler.ScheduleBatchAsync(null, CancellationToken.None);
        var hooks = new WhatsAppReminderHooks(db, scheduler, query);
        var repository = new ProgramRepository(app, new ConfigurationBuilder().Build());
        var handler = new CompleteTaskCommandHandler(repository, null, null, NullLogger<CompleteTaskCommandHandler>.Instance, hooks);
        await handler.Handle(new CompleteTaskCommand(source.EnrollmentId, source.Today, TaskCode.podcast,
            Guid.NewGuid().ToString(), null, null, null, null), CancellationToken.None);
        var reminders = await db.Reminders.AsNoTracking().Where(r => r.Kind == ReminderKind.podcast || r.Kind == ReminderKind.racha).ToListAsync();
        Assert.Equal(2, reminders.Count);
        Assert.All(reminders, r => Assert.Equal(ReminderState.Cancelled, r.State));
    }

    [Fact]
    public async Task InitialAssessment_CanExistBeforeActiveEnrollment_AndExcludesOtherBatteries()
    {
        var source = await SeedAsync();
        await using var app = fixture.CreateAppDb();
        await app.ProgramEnrollments.Where(x => x.Id == source.EnrollmentId).ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, ProgramEnrollmentStatus.Paused));
        var query = new ProgramAccompanimentQuery(app);
        var snapshot = await query.GetSnapshotAsync([source.PatientId], clock.Now, CancellationToken.None);
        Assert.Empty(snapshot.ActivePatients);
        Assert.Equal(ReminderKind.evaluacion, Assert.Single(snapshot.Reasons).Kind);
        await app.HealthTestBatteries.Where(b => b.Code == "bateria-inicial").ExecuteUpdateAsync(set => set.SetProperty(b => b.AutoAssignOnPatientCreate, false));
        Assert.Empty((await query.GetSnapshotAsync([source.PatientId], clock.Now, CancellationToken.None)).Reasons);
    }

    [Fact]
    public async Task Retry_FailsTwiceThenSucceeds_WithThreeDurableDeliveries()
    {
        var source = await SeedAsync();
        await using var db = fixture.CreateDb();
        var reminder = NewReminder(source.PatientId);
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();
        var calls = 0;
        var sender = Substitute.For<IWhatsAppMessageSender>();
        sender.SendTemplateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++calls < 3 ? WhatsAppSendResult.Failed("429", WhatsAppFailureKind.Transient) : new WhatsAppSendResult("wamid.third"));
        for (var i = 0; i < 3; i++)
        {
            await using var attemptDb = fixture.CreateDb();
            await using var app = fixture.CreateAppDb();
            var processor = new WhatsAppReminderProcessor(attemptDb, new ProgramAccompanimentQuery(app), sender, settings, clock);
            Assert.True(await processor.ProcessNextAsync(CancellationToken.None));
            var saved = await attemptDb.Reminders.AsNoTracking().SingleAsync();
            if (i < 2) { Assert.Equal(ReminderState.Pending, saved.State); clock.Now = saved.NextAttemptAt; }
            else Assert.Equal(ReminderState.Completed, saved.State);
        }
        Assert.Equal(3, calls);
        var deliveries = await db.ReminderDeliveries.OrderBy(d => d.Attempt).ToListAsync();
        Assert.Equal(new[] { 1, 2, 3 }, deliveries.Select(d => d.Attempt));
        Assert.Equal("wamid.third", deliveries[2].Wamid);
        Assert.Equal(DeliveryState.Accepted, deliveries[2].State);
    }

    [Fact]
    public async Task PermanentError_Cancels_AndOptOutNeverSends()
    {
        var source = await SeedAsync();
        await using var db = fixture.CreateDb();
        db.Reminders.Add(NewReminder(source.PatientId));
        await db.SaveChangesAsync();
        var sender = Substitute.For<IWhatsAppMessageSender>();
        sender.SendTemplateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(WhatsAppSendResult.Failed("190", WhatsAppFailureKind.Permanent));
        await using var app = fixture.CreateAppDb();
        var processor = new WhatsAppReminderProcessor(db, new ProgramAccompanimentQuery(app), sender, settings, clock);
        await processor.ProcessNextAsync(CancellationToken.None);
        Assert.Equal(ReminderState.Cancelled, (await db.Reminders.AsNoTracking().SingleAsync()).State);
        Assert.False(await processor.ProcessNextAsync(CancellationToken.None));
        await sender.Received(1).SendTemplateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        await db.PatientLinks.ExecuteUpdateAsync(set => set.SetProperty(x => x.OptOut, true));
        db.Reminders.Add(NewReminder(source.PatientId));
        await db.SaveChangesAsync();
        await processor.ProcessNextAsync(CancellationToken.None);
        await sender.Received(1).SendTemplateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TransientErrors_StopAtFiveAttempts()
    {
        var source = await SeedAsync();
        await using var db = fixture.CreateDb();
        db.Reminders.Add(NewReminder(source.PatientId));
        await db.SaveChangesAsync();
        var sender = Substitute.For<IWhatsAppMessageSender>();
        sender.SendTemplateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(WhatsAppSendResult.Failed("503", WhatsAppFailureKind.Transient));
        for (var i = 0; i < 5; i++)
        {
            await using var attemptDb = fixture.CreateDb();
            await using var app = fixture.CreateAppDb();
            await new WhatsAppReminderProcessor(attemptDb, new ProgramAccompanimentQuery(app), sender, settings, clock).ProcessNextAsync(CancellationToken.None);
            clock.Now = (await attemptDb.Reminders.AsNoTracking().SingleAsync()).NextAttemptAt;
        }
        Assert.Equal(ReminderState.Exhausted, (await db.Reminders.AsNoTracking().SingleAsync()).State);
        Assert.Equal(5, await db.ReminderDeliveries.CountAsync());
    }

    [Fact]
    public async Task AcceptedCheckin_DefersNextOneWithoutConsumingAttemptBefore24Hours()
    {
        var source = await SeedAsync();
        await using var db = fixture.CreateDb();
        var previous = NewReminder(source.PatientId);
        previous.State = ReminderState.Completed;
        db.Reminders.Add(previous);
        db.ReminderDeliveries.Add(new ReminderDelivery { ReminderId = previous.Id, PatientId = source.PatientId,
            Kind = ReminderKind.checkin, Attempt = 1, AttemptedAt = clock.Now.AddHours(-20), AcceptedAt = clock.Now.AddHours(-20), State = DeliveryState.Accepted, Wamid = "wamid.previous" });
        var next = NewReminder(source.PatientId);
        db.Reminders.Add(next);
        await db.SaveChangesAsync();
        var sender = Substitute.For<IWhatsAppMessageSender>();
        await using var app = fixture.CreateAppDb();
        await new WhatsAppReminderProcessor(db, new ProgramAccompanimentQuery(app), sender, settings, clock).ProcessNextAsync(CancellationToken.None);
        var saved = await db.Reminders.AsNoTracking().SingleAsync(x => x.Id == next.Id);
        Assert.True(saved.NextAttemptAt >= clock.Now.AddHours(4));
        Assert.Equal(0, saved.Attempts);
        Assert.Single(await db.ReminderDeliveries.ToListAsync());
        await sender.DidNotReceive().SendTemplateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConcurrentWorkers_ClaimOneAttempt()
    {
        var source = await SeedAsync();
        await using (var db = fixture.CreateDb()) { db.Reminders.Add(NewReminder(source.PatientId)); await db.SaveChangesAsync(); }
        var sender = Substitute.For<IWhatsAppMessageSender>();
        sender.SendTemplateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(new WhatsAppSendResult("wamid.concurrent"));
        async Task Run()
        {
            await using var db = fixture.CreateDb();
            await using var app = fixture.CreateAppDb();
            await new WhatsAppReminderProcessor(db, new ProgramAccompanimentQuery(app), sender, settings, clock).ProcessNextAsync(CancellationToken.None);
        }
        await Task.WhenAll(Run(), Run());
        await using var final = fixture.CreateDb();
        Assert.Single(await final.ReminderDeliveries.ToListAsync());
        await sender.Received(1).SendTemplateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    private Reminder NewReminder(Guid patient) => new() { PatientId = patient, Kind = ReminderKind.checkin,
        SourceKey = "test:" + Guid.NewGuid(), Detail = "Actualización del programa", ScheduledAt = clock.Now.AddHours(-3), NextAttemptAt = clock.Now };

    private async Task<Sources> SeedAsync()
    {
        await using var app = fixture.CreateAppDb();
        var patient = new PatientProfile { Id = Guid.NewGuid(), FirstName = "Ana", LastName = "Prueba", Status = "Activo" };
        var template = new ProgramTemplate { Id = Guid.NewGuid(), Code = Guid.NewGuid().ToString("N"), Name = "Programa QA", Status = TemplateStatus.Active };
        var enrollment = new ProgramEnrollment { Id = Guid.NewGuid(), Patient = patient, Template = template, StartLocalDate = new(2026, 11, 9) };
        var today = new DateOnly(2026, 11, 12);
        var media = new MediaItem { Id = Guid.NewGuid(), Title = "Respiración consciente", Author = "Equipo", StorageKey = "media/podcast-real.mp3",
            MediaType = MediaType.Podcast, Category = MediaCategory.Mindfulness, Status = MediaStatus.Published, CreatedAt = clock.Now };
        var week = new ProgramWeek { Id = Guid.NewGuid(), Enrollment = enrollment, WeekNumber = 1, Status = ProgramWeekStatus.Active,
            WeekStartDateLocal = new(2026, 11, 9), WeekEndDateLocal = new(2026, 11, 15), TemplateVersionAtStart = 1,
            TasksSnapshot = WeekSnapshotJson.Serialize([new WeekSnapshotTaskRow(4, "podcast", 10, 1, MediaId: media.Id)]) };
        app.ProgramWeeks.Add(week);
        app.MediaItems.Add(media);
        app.StreakStates.Add(new StreakState { Enrollment = enrollment, CurrentStreak = 3, LongestStreak = 3, LastActiveDate = today.AddDays(-1) });
        var battery = await app.HealthTestBatteries.SingleOrDefaultAsync(x => x.Code == "bateria-inicial")
            ?? new HealthTestBattery { Id = Guid.NewGuid(), Code = "bateria-inicial", Name = "Inicial", IsActive = true, AutoAssignOnPatientCreate = true };
        battery.AutoAssignOnPatientCreate = true;
        if (app.Entry(battery).State == EntityState.Detached) app.HealthTestBatteries.Add(battery);
        var instrument = new HealthTestInstrument { Id = Guid.NewGuid(), Code = Guid.NewGuid().ToString("N"), Name = "QA", Category = "clinico", IsActive = true };
        var version = new HealthTestVersion { Id = Guid.NewGuid(), Instrument = instrument, VersionNumber = 1, Status = HealthTestVersionStatus.active, IsCurrent = true };
        var batteryAssignment = new HealthTestBatteryAssignment { Id = Guid.NewGuid(), Patient = patient, Battery = battery };
        var assignment = new HealthTestAssignment { Id = Guid.NewGuid(), Patient = patient, Version = version, BatteryAssignment = batteryAssignment };
        app.HealthTestAssignments.Add(assignment);
        await app.SaveChangesAsync(CancellationToken.None);
        await using var db = fixture.CreateDb();
        db.PatientLinks.Add(new PatientLink { PatientId = patient.Id, WaId = "+573053924819", FirstName = "Ana", CreatedAt = clock.Now.AddDays(-2), UpdatedAt = clock.Now.AddDays(-2) });
        await db.SaveChangesAsync();
        return new(patient.Id, enrollment.Id, week.Id, media.Id, assignment.Id, today);
    }

    private sealed record Sources(Guid PatientId, Guid EnrollmentId, Guid WeekId, Guid MediaId, Guid AssignmentId, DateOnly Today);
}
