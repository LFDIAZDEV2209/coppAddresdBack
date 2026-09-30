using System.Text.Json;
using CoppAddresd.Application.Features.ProgramProgress.Commands.BulkAssignProgramContent;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.UnitTests.ProgramProgress.Handlers;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Media;

/// <summary>
/// Asignación masiva de podcasts en programas (change erp-program-content-admin,
/// Fase 4, REQ-PCA-04 / design D2): propagación por plantilla o inscripción
/// respetando la inmutabilidad de las semanas congeladas (Completed o con
/// fecha de cierre ya ocurrida) salvo <c>forceFrozen</c>, actualización
/// correcta de los snapshots de semanas futuras y auditoría crítica del
/// forzado. Handler con fakes en memoria (sin BD).
/// </summary>
public sealed class BulkAssignProgramContentTests
{
    private readonly IMediaItemRepository _mediaRepository = Substitute.For<IMediaItemRepository>();
    private readonly FakeProgramRepository _programRepository = new();

    private BulkAssignProgramContentCommandHandler CreateHandler() =>
        new(
            _mediaRepository,
            _programRepository,
            NullLogger<BulkAssignProgramContentCommandHandler>.Instance
        );

    private static MediaItem PublishedPodcast(MediaStatus status = MediaStatus.Published) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = "Introducción al Ritmo Circadiano",
            Author = "Dra. Elena Ramos",
            MediaType = MediaType.Podcast,
            Category = MediaCategory.Biologia,
            StorageKey = "media/podcasts/circadiano.mp3",
            ContentType = "audio/mpeg",
            FileSizeBytes = 15_485_760,
            DurationSecs = 930,
            Status = status,
            SortOrder = 1,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    /// <summary>Inscripción con semanas sembradas: weekNumbers congelados (Completed) y futuros (Locked).</summary>
    private Guid SeedEnrollment(int frozenWeeks = 2, int futureWeeks = 4, Guid? rowMediaId = null)
    {
        var enrollment = new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            TemplateId = Guid.NewGuid(),
        };
        _programRepository.Enrollments[enrollment.Id] = enrollment;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var week = 1; week <= frozenWeeks + futureWeeks; week++)
        {
            var isFrozen = week <= frozenWeeks;
            _programRepository.MediaAssignmentWeeks[(enrollment.Id, week)] = (
                isFrozen ? ProgramWeekStatus.Completed : ProgramWeekStatus.Locked,
                isFrozen ? today.AddDays(-7 * week) : today.AddDays(7 * week),
                [
                    // Podcast del lunes (con media previo distinto para verificar reemplazo).
                    new WeekSnapshotTaskRow(
                        1,
                        "podcast",
                        80,
                        1,
                        MediaId: rowMediaId ?? Guid.NewGuid()
                    ),
                    // Podcast del miércoles.
                    new WeekSnapshotTaskRow(
                        3,
                        "podcast",
                        80,
                        1,
                        MediaId: rowMediaId ?? Guid.NewGuid()
                    ),
                    // Tareas no podcast: nunca se tocan.
                    new WeekSnapshotTaskRow(1, "nut", 150, 2),
                    new WeekSnapshotTaskRow(2, "ejercicio", 150, 1),
                ]
            );
        }

        return enrollment.Id;
    }

    // ===================== Validaciones de entrada =====================

    [Fact]
    public async Task Validator_TargetTypeInvalido_Rechaza()
    {
        var validator = new BulkAssignProgramContentCommandValidator();
        var command = new BulkAssignProgramContentCommand(
            "Programa",
            Guid.NewGuid(),
            1,
            2,
            [1],
            Guid.NewGuid()
        );

        Assert.False((await validator.ValidateAsync(command)).IsValid);
    }

    [Fact]
    public async Task Validator_TargetTypeCaseInsensitive_Acepta()
    {
        var validator = new BulkAssignProgramContentCommandValidator();
        var command = new BulkAssignProgramContentCommand(
            "  enrollment ",
            Guid.NewGuid(),
            1,
            2,
            [1],
            Guid.NewGuid()
        );

        Assert.True((await validator.ValidateAsync(command)).IsValid);
    }

    [Fact]
    public async Task Validator_WeekdaysFueraDeRango_Rechaza()
    {
        var validator = new BulkAssignProgramContentCommandValidator();
        var command = new BulkAssignProgramContentCommand(
            "Template",
            Guid.NewGuid(),
            1,
            2,
            [0, 8],
            Guid.NewGuid()
        );

        Assert.False((await validator.ValidateAsync(command)).IsValid);
    }

    [Fact]
    public async Task Validator_ToWeekMenorQueFromWeek_Rechaza()
    {
        var validator = new BulkAssignProgramContentCommandValidator();
        var command = new BulkAssignProgramContentCommand(
            "Template",
            Guid.NewGuid(),
            5,
            3,
            [1],
            Guid.NewGuid()
        );

        Assert.False((await validator.ValidateAsync(command)).IsValid);
    }

    [Fact]
    public async Task Validator_WeekdaysVacios_Rechaza()
    {
        var validator = new BulkAssignProgramContentCommandValidator();
        var command = new BulkAssignProgramContentCommand(
            "Template",
            Guid.NewGuid(),
            1,
            2,
            [],
            Guid.NewGuid()
        );

        Assert.False((await validator.ValidateAsync(command)).IsValid);
    }

    // ===================== Guardias del medio y del objetivo =====================

    [Fact]
    public async Task Handle_MedioInexistente_LanzaNotFound()
    {
        _mediaRepository.GetByIdAsync(Arg.Any<Guid>()).Returns((MediaItem?)null);

        var command = new BulkAssignProgramContentCommand(
            "Template",
            Guid.NewGuid(),
            1,
            2,
            [1],
            Guid.NewGuid()
        );

        await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().Handle(command, default));
    }

    [Fact]
    public async Task Handle_MedioNoPublicado_LanzaUnprocessableEntity()
    {
        var media = PublishedPodcast(MediaStatus.Draft);
        _mediaRepository.GetByIdAsync(media.Id).Returns(media);

        var command = new BulkAssignProgramContentCommand(
            "Template",
            Guid.NewGuid(),
            1,
            2,
            [1],
            media.Id
        );

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            CreateHandler().Handle(command, default)
        );
    }

    [Fact]
    public async Task Handle_PlantillaInexistente_LanzaNotFound()
    {
        var media = PublishedPodcast();
        _mediaRepository.GetByIdAsync(media.Id).Returns(media);

        var command = new BulkAssignProgramContentCommand(
            "Template",
            Guid.NewGuid(),
            1,
            2,
            [1],
            media.Id
        );

        await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().Handle(command, default));
    }

    [Fact]
    public async Task Handle_InscripcionInexistente_LanzaNotFound()
    {
        var media = PublishedPodcast();
        _mediaRepository.GetByIdAsync(media.Id).Returns(media);

        var command = new BulkAssignProgramContentCommand(
            "Enrollment",
            Guid.NewGuid(),
            1,
            2,
            [1],
            media.Id
        );

        await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().Handle(command, default));
    }

    // ===================== Plantilla =====================

    [Fact]
    public async Task Handle_TargetTemplate_ActualizaReglasPodcastDeLosDias()
    {
        var media = PublishedPodcast();
        _mediaRepository.GetByIdAsync(media.Id).Returns(media);

        var template = new ProgramTemplate
        {
            Id = Guid.NewGuid(),
            Code = "tpl-test",
            Name = "Programa 83 Días",
            DayTemplates =
            [
                new WeeklyDayTemplate
                {
                    Weekday = 1,
                    TaskCode = TaskCode.podcast,
                    Points = 80,
                },
                new WeeklyDayTemplate
                {
                    Weekday = 1,
                    TaskCode = TaskCode.nut,
                    Points = 150,
                },
                new WeeklyDayTemplate
                {
                    Weekday = 3,
                    TaskCode = TaskCode.podcast,
                    Points = 80,
                },
                new WeeklyDayTemplate
                {
                    Weekday = 5,
                    TaskCode = TaskCode.podcast,
                    Points = 80,
                },
            ],
        };
        _programRepository.Templates[template.Id] = template;

        var command = new BulkAssignProgramContentCommand(
            "Template",
            template.Id,
            1,
            12,
            [1, 3],
            media.Id,
            ActorId: Guid.NewGuid()
        );

        var result = await CreateHandler().Handle(command, default);

        // Solo las filas podcast de lunes y miércoles (2 de 4 filas).
        Assert.True(result.Success);
        Assert.Equal(2, result.UpdatedWeeks);
        Assert.Equal(0, result.FrozenWeeksSkipped);
        Assert.Equal(0, result.AffectedEnrollments);
        Assert.Equal(12, result.TotalWeeksTargeted);
        Assert.Equal(2, _programRepository.TemplateMediaAssignments.Count);
        // La fila nut del lunes no se toca.
        Assert.Null(template.DayTemplates.Single(d => d.TaskCode == TaskCode.nut).MediaId);
    }

    // ===================== Inscripción: propagación D2 =====================

    [Fact]
    public async Task Handle_SemanasCongeladas_NoSeMutanSinForceFrozen()
    {
        var media = PublishedPodcast();
        _mediaRepository.GetByIdAsync(media.Id).Returns(media);
        var originalMediaId = Guid.NewGuid();
        var enrollmentId = SeedEnrollment(rowMediaId: originalMediaId);

        var command = new BulkAssignProgramContentCommand(
            "Enrollment",
            enrollmentId,
            1,
            6,
            [1, 3],
            media.Id,
            ForceFrozen: false
        );

        var result = await CreateHandler().Handle(command, default);

        // Semanas 1-2 congeladas omitidas; 3-6 futuras actualizadas.
        Assert.True(result.Success);
        Assert.Equal(6, result.TotalWeeksTargeted);
        Assert.Equal(4, result.UpdatedWeeks);
        Assert.Equal(2, result.FrozenWeeksSkipped);
        Assert.Equal(1, result.AffectedEnrollments);
        Assert.Equal(4, _programRepository.SavedWeekSnapshots.Count);
        Assert.DoesNotContain(_programRepository.SavedWeekSnapshots, u => u.WeekNumber <= 2);

        // El snapshot de la semana congelada permanece con el medio original.
        var frozenRows = _programRepository.MediaAssignmentWeeks[(enrollmentId, 1)].Rows;
        Assert.All(
            frozenRows.Where(r => r.TaskCode == "podcast"),
            r => Assert.Equal(originalMediaId, r.MediaId)
        );
    }

    [Fact]
    public async Task Handle_SemanasFuturas_ActualizanSnapshotSoloEnPodcastYDiasElegidos()
    {
        var media = PublishedPodcast();
        _mediaRepository.GetByIdAsync(media.Id).Returns(media);
        var enrollmentId = SeedEnrollment();

        var command = new BulkAssignProgramContentCommand(
            "Enrollment",
            enrollmentId,
            1,
            6,
            [1],
            media.Id
        );

        var result = await CreateHandler().Handle(command, default);

        Assert.Equal(4, result.UpdatedWeeks);
        Assert.Equal(4, _programRepository.SavedWeekSnapshots.Count);

        // Snapshot semana 3 re-serializado: podcast del lunes con el nuevo
        // medio, podcast del miércoles y tareas no-podcast intactas.
        var snapshot = _programRepository
            .SavedWeekSnapshots.Single(u => u.WeekNumber == 3)
            .Snapshot;
        Assert.Equal(JsonValueKind.Array, snapshot.ValueKind);
        var rows = WeekSnapshotJson.Parse(snapshot);
        Assert.Equal(4, rows.Count);
        Assert.Equal(media.Id, rows.Single(r => r.Weekday == 1 && r.TaskCode == "podcast").MediaId);
        Assert.Null(rows.Single(r => r.Weekday == 1 && r.TaskCode == "nut").MediaId);
        // El podcast del miércoles no está en los días elegidos: conserva su medio previo.
        Assert.NotEqual(
            media.Id,
            rows.Single(r => r.Weekday == 3 && r.TaskCode == "podcast").MediaId
        );
        // Shape canónico §3.4 preservado (snake_case).
        Assert.Equal(
            JsonValueKind.Array,
            JsonDocument.Parse(snapshot.GetRawText()).RootElement.ValueKind
        );
    }

    [Fact]
    public async Task Handle_FilasYaAsignadas_IdempotenteNoReescribe()
    {
        var media = PublishedPodcast();
        _mediaRepository.GetByIdAsync(media.Id).Returns(media);
        var enrollmentId = SeedEnrollment(rowMediaId: media.Id);

        var command = new BulkAssignProgramContentCommand(
            "Enrollment",
            enrollmentId,
            1,
            6,
            [1, 3],
            media.Id
        );

        var result = await CreateHandler().Handle(command, default);

        // Todas las filas podcast ya tenían el medio: nada que actualizar
        // (idempotente), y las semanas 1-2 congeladas se omiten igual.
        Assert.Equal(0, result.UpdatedWeeks);
        Assert.Equal(2, result.FrozenWeeksSkipped);
        Assert.Empty(_programRepository.SavedWeekSnapshots);
        Assert.Empty(_programRepository.AuditRows);
    }

    [Fact]
    public async Task Handle_ForceFrozenTrue_MutaCongeladas_YAuditaCritico()
    {
        var media = PublishedPodcast();
        _mediaRepository.GetByIdAsync(media.Id).Returns(media);
        var enrollmentId = SeedEnrollment();

        var actorId = Guid.NewGuid();
        var command = new BulkAssignProgramContentCommand(
            "Enrollment",
            enrollmentId,
            1,
            6,
            [1, 3],
            media.Id,
            ForceFrozen: true,
            ActorId: actorId
        );

        var result = await CreateHandler().Handle(command, default);

        // Las 6 semanas (incluidas 2 congeladas) se actualizaron.
        Assert.Equal(6, result.UpdatedWeeks);
        Assert.Equal(0, result.FrozenWeeksSkipped);
        Assert.Equal(6, _programRepository.SavedWeekSnapshots.Count);

        // Evento de auditoría crítico del forzado (REQ-PCA-04).
        var auditRow = Assert.Single(_programRepository.AuditRows);
        Assert.Equal("BulkAssignForceFrozen", auditRow.Action);
        Assert.Equal("program_weeks", auditRow.TableName);
        Assert.Equal("app", auditRow.SchemaName);
        Assert.Equal(enrollmentId, auditRow.RecordId);
        Assert.Equal(actorId, auditRow.ActorId);

        // El snapshot de la semana congelada quedó con el nuevo medio.
        var frozenRows = _programRepository.MediaAssignmentWeeks[(enrollmentId, 1)].Rows;
        Assert.All(
            frozenRows.Where(r => r.TaskCode == "podcast"),
            r => Assert.Equal(media.Id, r.MediaId)
        );
    }

    [Fact]
    public async Task Handle_ForceFrozenSinSemanasCongeladas_NoAuditaCritico()
    {
        var media = PublishedPodcast();
        _mediaRepository.GetByIdAsync(media.Id).Returns(media);
        // Todas las semanas futuras (Locked, sin congeladas).
        var enrollmentId = SeedEnrollment(frozenWeeks: 0, futureWeeks: 3);

        var command = new BulkAssignProgramContentCommand(
            "Enrollment",
            enrollmentId,
            1,
            3,
            [1],
            media.Id,
            ForceFrozen: true
        );

        await CreateHandler().Handle(command, default);

        Assert.Empty(_programRepository.AuditRows);
        Assert.Equal(3, _programRepository.SavedWeekSnapshots.Count);
    }
}
