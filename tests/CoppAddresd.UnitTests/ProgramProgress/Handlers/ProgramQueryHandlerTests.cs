using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetAdaptation;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetCalendar;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetPath;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetSnapshot;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListAdaptations;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListEnrollments;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListTemplates;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using System.Text.Json;

namespace CoppAddresd.UnitTests.ProgramProgress.Handlers;

/// <summary>
/// Tests de las queries de lectura (SPEC §7.1/§7.3/§7.4/§7.5/§7.6/§7.7):
/// delegación al repositorio, mapeo a los DTOs y 404 cuando el recurso no
/// existe.
/// </summary>
public class ProgramQueryHandlerTests
{
    private readonly FakeProgramRepository _repository = new();
    private readonly GetSnapshotQueryHandler _snapshotHandler;
    private readonly GetCalendarQueryHandler _calendarHandler;
    private readonly GetPathQueryHandler _pathHandler;
    private readonly ListTemplatesQueryHandler _listTemplatesHandler;
    private readonly GetTemplateQueryHandler _getTemplateHandler;
    private readonly ListEnrollmentsQueryHandler _listEnrollmentsHandler;
    private readonly ListAdaptationsQueryHandler _listAdaptationsHandler;
    private readonly GetAdaptationQueryHandler _getAdaptationHandler;
    private readonly Guid _enrollmentId = Guid.NewGuid();

    public ProgramQueryHandlerTests()
    {
        _snapshotHandler = new GetSnapshotQueryHandler(_repository);
        _calendarHandler = new GetCalendarQueryHandler(_repository);
        _pathHandler = new GetPathQueryHandler(_repository);
        _listTemplatesHandler = new ListTemplatesQueryHandler(_repository);
        _getTemplateHandler = new GetTemplateQueryHandler(_repository);
        _listEnrollmentsHandler = new ListEnrollmentsQueryHandler(_repository);
        _listAdaptationsHandler = new ListAdaptationsQueryHandler(_repository);
        _getAdaptationHandler = new GetAdaptationQueryHandler(_repository);
    }

    private static ProgramSnapshotDto SampleSnapshot(Guid enrollmentId) => new(
        enrollmentId,
        new ProgramSnapshotTemplateDto(
            Guid.NewGuid(), "default-83w", "Programa 83 semanas", 83, 12,
            ProgramWeekStatus.Active, new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27),
            1, ["nut", "ejercicio", "nutraceutico"]),
        new DateOnly(2026, 9, 24),
        [
            new TodayTaskDto(TaskCode.podcast, "Escuchar podcast", "Biohacking · 8 min", 80,
                "Pending", null,
                new TodayTaskContentDto(Guid.NewGuid(), "Episodio 12", 492, "media/thumbnails/x.jpg")),
            new TodayTaskDto(TaskCode.vitals, "Medir signos vitales", "FC · SpO2", 120,
                "Completed", new DateTime(2026, 9, 24, 11, 14, 8, DateTimeKind.Utc), null),
        ],
        200, true, 750,
        new XpInfoDto(1620, "Constante", 3000),
        new StreakInfoDto(11, 27, 2, 1.0m, null, 0),
        16,
        [new CalendarDayDto(new DateOnly(2026, 9, 24), 4, false, 200, "InProgress")],
        null, 50);

    [Fact]
    public async Task Handle_Snapshot_DevuelveShape71()
    {
        _repository.PatientToday = new DateOnly(2026, 9, 24);
        _repository.OnGetSnapshot = (_, _) => Task.FromResult<ProgramSnapshotDto?>(
            SampleSnapshot(_enrollmentId));

        var dto = await _snapshotHandler.Handle(
            new GetSnapshotQuery(_enrollmentId), CancellationToken.None);

        Assert.Equal(_enrollmentId, dto.EnrollmentId);
        Assert.Equal("default-83w", dto.Template.Code);
        Assert.Equal(ProgramWeekStatus.Active, dto.Template.CurrentWeekStatus);
        Assert.Equal(2, dto.TodayTasks.Count);
        var podcast = dto.TodayTasks[0];
        Assert.Equal(TaskCode.podcast, podcast.TaskCode);
        Assert.NotNull(podcast.Content);
        Assert.Equal("Episodio 12", podcast.Content.Title);
        Assert.Equal(492, podcast.Content.DurationSecs);
        Assert.Equal("media/thumbnails/x.jpg", podcast.Content.ThumbnailUrl);
        Assert.Equal(1620, dto.Xp.Balance);
        Assert.Equal("Constante", dto.Xp.Level);
        Assert.Equal(11, dto.Streak.Current);
        // Campo aditivo (SPEC §7.1/§14): monto base real del bonus DAY_BONUS.
        Assert.Equal(50, dto.DailyBonusAmount);
    }

    [Fact]
    public async Task Handle_SnapshotSinInscripcion_LanzaNotFound()
    {
        // Sin inscripción (sin "hoy" del paciente) → 404 NO_ACTIVE_ENROLLMENT.
        _repository.PatientToday = null;

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _snapshotHandler.Handle(new GetSnapshotQuery(_enrollmentId), CancellationToken.None));
        Assert.Contains("NO_ACTIVE_ENROLLMENT", ex.Message);
    }

    [Fact]
    public async Task Handle_Calendario_DevuelveRollups()
    {
        var from = new DateOnly(2026, 9, 1);
        var to = new DateOnly(2026, 9, 30);
        _repository.OnGetCalendar = (_, _) => Task.FromResult(new ProgramCalendarDto(
            from, to,
            [new CalendarDayDetailDto(new DateOnly(2026, 9, 1), 2, 10, true, 750, 50,
                ["podcast", "vitals", "nut", "ejercicio", "nutraceutico", "emocional"])],
            new CalendarSummaryDto(18, 2, 1450)));

        var dto = await _calendarHandler.Handle(
            new GetCalendarQuery(_enrollmentId, from, to), CancellationToken.None);

        Assert.Equal(from, dto.From);
        Assert.Equal(to, dto.To);
        var day = Assert.Single(dto.Days);
        Assert.True(day.IsPerfectDay);
        Assert.Equal(50, day.BonusAwarded);
        Assert.Equal(6, day.CompletedTaskCodes.Count);
        Assert.Equal(18, dto.Summary.PerfectDays);
    }

    [Fact]
    public async Task Handle_Path_DevuelveSemanas()
    {
        _repository.OnGetPath = (_, _) => Task.FromResult(new ProgramPathDto(
        [
            new ProgramPathWeekDto(1, ProgramWeekStatus.Completed, true, 5250,
                new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 12)),
            new ProgramPathWeekDto(12, ProgramWeekStatus.Active, null, 1820,
                new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27)),
            new ProgramPathWeekDto(13, ProgramWeekStatus.Locked, null, 0,
                new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 4)),
        ]));

        var dto = await _pathHandler.Handle(new GetPathQuery(_enrollmentId), CancellationToken.None);

        Assert.Equal(3, dto.Weeks.Count);
        Assert.Equal(ProgramWeekStatus.Completed, dto.Weeks[0].Status);
        Assert.True(dto.Weeks[0].IsPerfectWeek);
        Assert.Null(dto.Weeks[1].IsPerfectWeek); // Active no define isPerfectWeek
        Assert.Equal(ProgramWeekStatus.Locked, dto.Weeks[2].Status);
    }

    [Fact]
    public async Task Handle_ListarPlantillas_Pagina()
    {
        var t = new ProgramTemplate
        {
            Id = Guid.NewGuid(), Code = "default-83w", Name = "Programa 83 semanas",
            TotalWeeks = 83, Status = TemplateStatus.Active, Version = 1,
            CreatedAt = DateTime.UtcNow,
        };
        _repository.Templates[t.Id] = t;

        var result = await _listTemplatesHandler.Handle(
            new ListTemplatesQuery(Search: null, Status: TemplateStatus.Active), CancellationToken.None);

        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.TotalPages);
        var item = Assert.Single(result.Data);
        Assert.Equal("default-83w", item.Code);
        Assert.Equal(TemplateStatus.Active, item.Status);
    }

    [Fact]
    public async Task Handle_GetPlantillaInexistente_LanzaNotFound()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _getTemplateHandler.Handle(new GetTemplateQuery(Guid.NewGuid()), CancellationToken.None));
        Assert.Contains("no encontrada", ex.Message);
    }

    [Fact]
    public async Task Handle_ListarInscripciones_FiltraPorEstado()
    {
        _repository.Enrollments[_enrollmentId] = new ProgramEnrollment
        {
            Id = _enrollmentId, PatientId = Guid.NewGuid(), TemplateId = Guid.NewGuid(),
            Timezone = "America/Bogota", Status = ProgramEnrollmentStatus.Paused,
            StartLocalDate = new DateOnly(2026, 9, 21), CreatedAt = DateTime.UtcNow,
        };

        var result = await _listEnrollmentsHandler.Handle(
            new ListEnrollmentsQuery(Status: ProgramEnrollmentStatus.Paused), CancellationToken.None);

        var item = Assert.Single(result.Data);
        Assert.Equal(ProgramEnrollmentStatus.Paused, item.Status);
        Assert.Equal("America/Bogota", item.Timezone);
    }

    [Fact]
    public async Task Handle_ListarAdaptaciones_PaginaYFiltra()
    {
        _repository.Adaptations[Guid.NewGuid()] = new AdaptationRecommendation
        {
            Id = Guid.NewGuid(), EnrollmentId = _enrollmentId,
            Kind = AdaptationKind.DifficultyChange,
            TargetEntityType = AdaptationTargetEntityType.WeeklyDayTemplates,
            TargetEntityId = Guid.NewGuid(),
            Payload = JsonSerializer.SerializeToElement(new { points = 120 }),
            Reason = "AC-16", Status = AdaptationStatus.Pending, RequiresApproval = true,
            CreatedAt = DateTime.UtcNow,
        };

        var result = await _listAdaptationsHandler.Handle(
            new ListAdaptationsQuery(EnrollmentId: _enrollmentId, Status: AdaptationStatus.Pending), CancellationToken.None);

        var item = Assert.Single(result.Data);
        Assert.Equal(AdaptationKind.DifficultyChange, item.Kind);
        Assert.True(item.RequiresApproval);
        Assert.Equal(AdaptationStatus.Pending, item.Status);
        Assert.Equal(JsonValueKind.Object, item.Payload.ValueKind); // jsonb opaco
    }

    [Fact]
    public async Task Handle_GetAdaptacionInexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _getAdaptationHandler.Handle(new GetAdaptationQuery(Guid.NewGuid()), CancellationToken.None));
    }
}