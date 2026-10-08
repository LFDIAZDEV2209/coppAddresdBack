using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using MediatR;
using CoppAddresd.Domain.Exceptions;

namespace CoppAddresd.Application.Features.HealthTests.Catalog;

public record SaveDraftQuestionCommand(Guid VersionId, HealthTestQuestionDto Question) : IRequest<HealthTestQuestionDto>;
/// <summary>Solo los borradores permiten editar preguntas; las evaluaciones históricas conservan su versión.</summary>
public sealed class SaveDraftQuestionCommandHandler(IHealthTestRepository repository)
    : IRequestHandler<SaveDraftQuestionCommand, HealthTestQuestionDto>
{
    public async Task<HealthTestQuestionDto> Handle(SaveDraftQuestionCommand request, CancellationToken ct)
    {
        var version = await repository.GetVersionWithDetailsAsync(request.VersionId, ct)
            ?? throw new NotFoundException("Versión no encontrada.");
        if (version.Status != HealthTestVersionStatus.draft) throw new BusinessRuleViolationException("Clona la versión publicada antes de editar sus preguntas.");
        var q = request.Question;
        if (string.IsNullOrWhiteSpace(q.Code) || string.IsNullOrWhiteSpace(q.Text) || q.Code.Length > 100 || q.Text.Length > 5000)
            throw new UnprocessableEntityException("Código y texto de pregunta son requeridos.");
        if (q.Id != Guid.Empty && !version.Questions.Any(existing => existing.Id == q.Id)) throw new UnprocessableEntityException("La pregunta no pertenece al borrador.");
        if (version.Questions.Any(existing => existing.Id != q.Id && existing.Code == q.Code.Trim())) throw new UnprocessableEntityException("Código de pregunta duplicado.");
        if (q.Options.Count > 100 || q.Options.Any(option => string.IsNullOrWhiteSpace(option.Text))) throw new UnprocessableEntityException("Opciones inválidas.");
        if (q.MinValue > q.MaxValue) throw new UnprocessableEntityException("Rango numérico inválido.");
        if (!Enum.IsDefined(q.Type) || !Enum.IsDefined(q.ScoringDirection)) throw new UnprocessableEntityException("Tipo de pregunta inválido.");
        var existingOptions = version.Questions.FirstOrDefault(existing => existing.Id == q.Id)?.Options.Select(o => o.Id).ToHashSet() ?? [];
        if (q.Options.Any(o => o.Id != Guid.Empty && !existingOptions.Contains(o.Id))) throw new UnprocessableEntityException("La opción no pertenece a la pregunta.");
        if (q.Options.Where(o => o.Id != Guid.Empty).GroupBy(o => o.Id).Any(g => g.Count() > 1)) throw new UnprocessableEntityException("Opciones duplicadas.");
        var versionQuestionIds = version.Questions.Select(x => x.Id).ToHashSet();
        var versionOptionIds = version.Questions.SelectMany(x => x.Options).Select(x => x.Id).ToHashSet();
        if (q.Options.Any(o => (o.DependsOnQuestionId is {} qid && !versionQuestionIds.Contains(qid)) || (o.DependsOnOptionId is {} oid && !versionOptionIds.Contains(oid)))) throw new UnprocessableEntityException("Dependencia fuera de la versión.");
        var entity = new HealthTestQuestion
        {
            Id = q.Id == Guid.Empty ? Guid.NewGuid() : q.Id, VersionId = request.VersionId,
            Code = q.Code.Trim(), Text = q.Text.Trim(), Section = q.Section, Type = q.Type, ScoringDirection = q.ScoringDirection,
            SortOrder = q.SortOrder, IsActive = q.IsActive, Unit = q.Unit, MinValue = q.MinValue, MaxValue = q.MaxValue,
            DefaultValue = q.DefaultValue, MinLabel = q.MinLabel, MaxLabel = q.MaxLabel, Hint = q.Hint,
        };
        entity.Options = q.Options.Select(option => new HealthTestAnswerOption
        {
            Id = option.Id == Guid.Empty ? Guid.NewGuid() : option.Id, QuestionId = entity.Id, Text = option.Text,
            ScoreValue = option.ScoreValue, SortOrder = option.SortOrder, IsActive = option.IsActive,
            DependsOnQuestionId = option.DependsOnQuestionId, DependsOnOptionId = option.DependsOnOptionId,
        }).ToList();
        await repository.SaveQuestionAsync(entity, ct);
        return HealthTestQuestionDto.FromEntity(entity);
    }
}
