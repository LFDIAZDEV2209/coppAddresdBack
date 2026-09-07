using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ArchiveTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Commands.CreateTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Commands.PublishTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ReplaceWeekdayTasks;
using CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateTemplate;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests.ProgramProgress.Handlers;

/// <summary>
/// Tests de los comandos de plantillas (SPEC §7.6, permiso Program.Edit en la
/// API): creación en Draft, actualización preservando el ciclo de vida,
/// publicación que incrementa versión, archivado y reemplazo en bloque del
/// horario semanal.
/// </summary>
public class TemplateCommandHandlerTests
{
    private readonly FakeProgramRepository _repository = new();
    private readonly CreateTemplateCommandHandler _createHandler;
    private readonly UpdateTemplateCommandHandler _updateHandler;
    private readonly PublishTemplateCommandHandler _publishHandler;
    private readonly ArchiveTemplateCommandHandler _archiveHandler;
    private readonly ReplaceWeekdayTasksCommandHandler _replaceHandler;
    private readonly Guid _templateId = Guid.NewGuid();

    public TemplateCommandHandlerTests()
    {
        _createHandler = new CreateTemplateCommandHandler(
            _repository, NullLogger<CreateTemplateCommandHandler>.Instance);
        _updateHandler = new UpdateTemplateCommandHandler(
            _repository, NullLogger<UpdateTemplateCommandHandler>.Instance);
        _publishHandler = new PublishTemplateCommandHandler(
            _repository, NullLogger<PublishTemplateCommandHandler>.Instance);
        _archiveHandler = new ArchiveTemplateCommandHandler(
            _repository, NullLogger<ArchiveTemplateCommandHandler>.Instance);
        _replaceHandler = new ReplaceWeekdayTasksCommandHandler(
            _repository, NullLogger<ReplaceWeekdayTasksCommandHandler>.Instance);

        _repository.Templates[_templateId] = new ProgramTemplate
        {
            Id = _templateId,
            Code = "default-83w",
            Name = "Programa 83 semanas",
            TotalWeeks = 83,
            Status = TemplateStatus.Draft,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            DayTemplates =
            [
                new WeeklyDayTemplate { Id = Guid.NewGuid(), TemplateId = _templateId, Weekday = 1, TaskCode = TaskCode.podcast, Points = 80, SortOrder = 1 },
            ],
        };
    }

    private static IReadOnlyList<WeeklyDayTemplateRequest> TwoDays() =>
    [
        new(1, TaskCode.podcast, 80, 1),
        new(1, TaskCode.vitals, 120, 2),
    ];

    [Fact]
    public async Task Handle_Crear_DevuelveDraftConDias()
    {
        var command = new CreateTemplateCommand(
            "custom-12w", "Programa 12 semanas", "Prueba", 12, TwoDays());

        var dto = await _createHandler.Handle(command, CancellationToken.None);

        Assert.Equal("custom-12w", dto.Code);
        Assert.Equal(TemplateStatus.Draft, dto.Status);
        Assert.Equal(1, dto.Version);
        Assert.Equal(2, dto.Days.Count);
        Assert.Equal(TaskCode.podcast, dto.Days[0].TaskCode);
        var stored = Assert.Single(_repository.Templates.Values, t => t.Code == "custom-12w");
        Assert.Equal(12, stored.TotalWeeks);
    }

    [Fact]
    public async Task Handle_Crear_SinDias_Genera42TareasPorDefecto()
    {
        var command = new CreateTemplateCommand(
            "default-auto", "Programa Auto", "Prueba sin días", 12);

        var dto = await _createHandler.Handle(command, CancellationToken.None);

        Assert.Equal("default-auto", dto.Code);
        Assert.Equal(TemplateStatus.Draft, dto.Status);
        Assert.Equal(42, dto.Days.Count); // 7 días × 6 tareas
        Assert.Equal(12, dto.TotalWeeks);
    }

    [Fact]
    public async Task Handle_Crear_ConTotalDays83_Calcula12Semanas()
    {
        var command = new CreateTemplateCommand(
            "plan-83d", "Plan 83 días", "Prueba 83 días", TotalDays: 83);

        var dto = await _createHandler.Handle(command, CancellationToken.None);

        Assert.Equal("plan-83d", dto.Code);
        Assert.Equal(12, dto.TotalWeeks); // ceil(83 / 7) = 12
        Assert.Equal(42, dto.Days.Count);
    }

    [Fact]
    public async Task Handle_Actualizar_SinDias_PreservaTareasExistentes()
    {
        var command = new UpdateTemplateCommand(
            _templateId, "default-83w", "Programa 83 semanas actualizado", null, TotalDays: 83);

        var dto = await _updateHandler.Handle(command, CancellationToken.None);

        Assert.Equal("Programa 83 semanas actualizado", dto.Name);
        Assert.Equal(12, dto.TotalWeeks); // ceil(83 / 7) = 12
        Assert.Single(dto.Days); // Conservó el día existente de _templateId
    }

    [Fact]
    public async Task Handle_Actualizar_PreservaEstadoYVersion()
    {
        var command = new UpdateTemplateCommand(
            _templateId, "default-83w", "Programa 83 semanas v2", null, 83, TwoDays());

        var dto = await _updateHandler.Handle(command, CancellationToken.None);

        Assert.Equal("Programa 83 semanas v2", dto.Name);
        Assert.Equal(TemplateStatus.Draft, dto.Status); // el ciclo de vida no cambia aquí
        Assert.Equal(1, dto.Version);                    // la versión no cambia aquí
        Assert.Equal(2, dto.Days.Count);                 // los días se reemplazan en bloque
    }

    [Fact]
    public async Task Handle_ActualizarInexistente_LanzaNotFound()
    {
        var command = new UpdateTemplateCommand(
            Guid.NewGuid(), "x", "X", null, 83, TwoDays());

        await Assert.ThrowsAsync<NotFoundException>(
            () => _updateHandler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Publicar_IncrementaVersionYActiva()
    {
        var dto = await _publishHandler.Handle(
            new PublishTemplateCommand(_templateId), CancellationToken.None);

        Assert.Equal(TemplateStatus.Active, dto.Status);
        Assert.Equal(2, dto.Version);
        Assert.NotNull(dto.PublishedAt);
    }

    [Fact]
    public async Task Handle_PublicarArchivada_LanzaViolacion()
    {
        _repository.Templates[_templateId].Status = TemplateStatus.Archived;

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => _publishHandler.Handle(new PublishTemplateCommand(_templateId), CancellationToken.None));
        Assert.Contains("TEMPLATE_STATE", ex.Message);
    }

    [Fact]
    public async Task Handle_Archivar_DevuelveArchivada()
    {
        var dto = await _archiveHandler.Handle(
            new ArchiveTemplateCommand(_templateId), CancellationToken.None);

        Assert.Equal(TemplateStatus.Archived, dto.Status);
    }

    [Fact]
    public async Task Handle_ArchivarInexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _archiveHandler.Handle(new ArchiveTemplateCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ReemplazarTareas_DevuelveFilasNuevas()
    {
        var command = new ReplaceWeekdayTasksCommand(
            _templateId, [new(2, TaskCode.nut, 150, 1), new(2, TaskCode.emocional, 120, 2)]);

        var dto = await _replaceHandler.Handle(command, CancellationToken.None);

        Assert.Equal(2, dto.Count);
        Assert.All(dto, d => Assert.Equal((short)2, d.Weekday));
        var stored = _repository.Templates[_templateId].DayTemplates;
        Assert.Equal(2, stored.Count);
        Assert.Equal(TaskCode.nut, stored.First().TaskCode);
    }

    [Fact]
    public async Task Handle_ReemplazarEnInexistente_LanzaNotFound()
    {
        var command = new ReplaceWeekdayTasksCommand(
            Guid.NewGuid(), [new(1, TaskCode.podcast, 80, 1)]);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _replaceHandler.Handle(command, CancellationToken.None));
    }
}