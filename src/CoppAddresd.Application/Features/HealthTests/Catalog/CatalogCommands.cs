using CoppAddresd.Application.Features.HealthTests;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using MediatR;
using CoppAddresd.Domain.Exceptions;

namespace CoppAddresd.Application.Features.HealthTests.Catalog;

// --- List Versions ---

public record ListVersionsQuery(Guid InstrumentId) : IRequest<IReadOnlyList<HealthTestVersionDto>>;

public sealed class ListVersionsQueryHandler(IHealthTestRepository repository)
    : IRequestHandler<ListVersionsQuery, IReadOnlyList<HealthTestVersionDto>>
{
    public async Task<IReadOnlyList<HealthTestVersionDto>> Handle(
        ListVersionsQuery request,
        CancellationToken ct
    )
    {
        var versions = await repository.ListVersionsByInstrumentAsync(request.InstrumentId, ct);
        return versions.Select(HealthTestVersionDto.FromEntity).ToList();
    }
}

// --- List Questions ---

public record ListQuestionsQuery(Guid VersionId) : IRequest<IReadOnlyList<HealthTestQuestionDto>>;

public sealed class ListQuestionsQueryHandler(IHealthTestRepository repository)
    : IRequestHandler<ListQuestionsQuery, IReadOnlyList<HealthTestQuestionDto>>
{
    public async Task<IReadOnlyList<HealthTestQuestionDto>> Handle(
        ListQuestionsQuery request,
        CancellationToken ct
    )
    {
        var questions = await repository.ListQuestionsByVersionAsync(request.VersionId, ct);
        return questions.Select(HealthTestQuestionDto.FromEntity).ToList();
    }
}

// --- List Instruments ---

public record ListInstrumentsQuery(
    string? Search = null,
    string? Category = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<PaginatedHealthTestsResult<HealthTestInstrumentDto>>;

public sealed class ListInstrumentsQueryHandler(IHealthTestRepository repository)
    : IRequestHandler<ListInstrumentsQuery, PaginatedHealthTestsResult<HealthTestInstrumentDto>>
{
    public async Task<PaginatedHealthTestsResult<HealthTestInstrumentDto>> Handle(
        ListInstrumentsQuery request,
        CancellationToken ct
    )
    {
        var (items, total) = await repository.ListInstrumentsAsync(
            request.Search,
            request.Category,
            request.IsActive,
            Math.Max(1, request.Page),
            Math.Clamp(request.PageSize, 1, 100),
            ct
        );
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return new PaginatedHealthTestsResult<HealthTestInstrumentDto>(
            items.Select(HealthTestInstrumentDto.FromEntity).ToList(),
            total,
            Math.Max(1, request.Page),
            pageSize,
            totalPages
        );
    }
}

// --- Get Instrument ---

public record GetInstrumentQuery(Guid Id) : IRequest<HealthTestInstrumentDto?>;

public sealed class GetInstrumentQueryHandler(IHealthTestRepository repository)
    : IRequestHandler<GetInstrumentQuery, HealthTestInstrumentDto?>
{
    public async Task<HealthTestInstrumentDto?> Handle(
        GetInstrumentQuery request,
        CancellationToken ct
    )
    {
        var instrument = await repository.GetInstrumentByIdAsync(request.Id, ct);
        return instrument is null ? null : HealthTestInstrumentDto.FromEntity(instrument);
    }
}

// --- Create Instrument ---

public record CreateInstrumentRequest(
    string Code,
    string Name,
    string? Description,
    string? Category,
    int SortOrder,
    bool IsActive = true
);

public record CreateInstrumentCommand(CreateInstrumentRequest Request, Guid? CreatedBy = null)
    : IRequest<HealthTestInstrumentDto>;

public sealed class CreateInstrumentCommandHandler(IHealthTestRepository repository)
    : IRequestHandler<CreateInstrumentCommand, HealthTestInstrumentDto>
{
    public async Task<HealthTestInstrumentDto> Handle(
        CreateInstrumentCommand request,
        CancellationToken ct
    )
    {
        var r = request.Request;
        if (string.IsNullOrWhiteSpace(r.Code) || string.IsNullOrWhiteSpace(r.Name)) throw new UnprocessableEntityException("Código y nombre son requeridos.");
        var existing = await repository.GetInstrumentByCodeAsync(r.Code.Trim(), ct);
        if (existing is not null)
        {
            throw new BusinessRuleViolationException(
                $"Ya existe un instrumento con el código '{r.Code}'."
            );
        }

        var instrument = new HealthTestInstrument
        {
            Id = Guid.NewGuid(),
            Code = r.Code.Trim(),
            Name = r.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim(),
            Category = string.IsNullOrWhiteSpace(r.Category) ? null : r.Category.Trim(),
            SortOrder = r.SortOrder,
            IsActive = r.IsActive,
            CreatedAt = DateTime.UtcNow,
        };

        await repository.AddInstrumentAsync(instrument, ct);
        return HealthTestInstrumentDto.FromEntity(instrument);
    }
}

// --- Update Instrument ---

public record UpdateInstrumentRequest(
    string Name,
    string? Description,
    string? Category,
    int SortOrder,
    bool IsActive
);

public record UpdateInstrumentCommand(Guid Id, UpdateInstrumentRequest Request)
    : IRequest<HealthTestInstrumentDto?>;

public sealed class UpdateInstrumentCommandHandler(IHealthTestRepository repository)
    : IRequestHandler<UpdateInstrumentCommand, HealthTestInstrumentDto?>
{
    public async Task<HealthTestInstrumentDto?> Handle(
        UpdateInstrumentCommand request,
        CancellationToken ct
    )
    {
        var instrument = await repository.GetInstrumentByIdAsync(request.Id, ct);
        if (instrument is null)
        {
            return null;
        }

        var r = request.Request;
        if (string.IsNullOrWhiteSpace(r.Name)) throw new UnprocessableEntityException("El nombre es requerido.");
        instrument.Name = r.Name.Trim();
        instrument.Description = string.IsNullOrWhiteSpace(r.Description)
            ? null
            : r.Description.Trim();
        instrument.Category = string.IsNullOrWhiteSpace(r.Category) ? null : r.Category.Trim();
        instrument.SortOrder = r.SortOrder;
        instrument.IsActive = r.IsActive;
        instrument.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateInstrumentAsync(instrument, ct);
        return HealthTestInstrumentDto.FromEntity(instrument);
    }
}

// --- Create Version (draft) ---

public record CreateVersionRequest(
    int VersionNumber,
    string? Name,
    HealthTestScoringStrategy ScoringStrategy,
    int? Points
);

public record CreateVersionCommand(Guid InstrumentId, CreateVersionRequest Request)
    : IRequest<HealthTestVersionDto>;

public sealed class CreateVersionCommandHandler(IHealthTestRepository repository)
    : IRequestHandler<CreateVersionCommand, HealthTestVersionDto>
{
    public async Task<HealthTestVersionDto> Handle(
        CreateVersionCommand request,
        CancellationToken ct
    )
    {
        var instrument = await repository.GetInstrumentByIdAsync(request.InstrumentId, ct);
        if (instrument is null)
        {
            throw new InvalidOperationException("El instrumento no existe.");
        }

        var r = request.Request;
        if (r.VersionNumber < 1 || !Enum.IsDefined(r.ScoringStrategy) || r.Points < 0) throw new UnprocessableEntityException("Configuración de versión inválida.");
        if ((await repository.ListVersionsByInstrumentAsync(request.InstrumentId, ct)).Any(v => v.VersionNumber == r.VersionNumber)) throw new UnprocessableEntityException("El número de versión ya existe.");
        var version = new HealthTestVersion
        {
            Id = Guid.NewGuid(),
            InstrumentId = request.InstrumentId,
            VersionNumber = r.VersionNumber,
            Name = r.Name,
            Status = HealthTestVersionStatus.draft,
            IsCurrent = false,
            ScoringStrategy = r.ScoringStrategy,
            Points = r.Points,
            CreatedAt = DateTime.UtcNow,
        };

        await repository.AddVersionAsync(version, ct);
        return HealthTestVersionDto.FromEntity(version);
    }
}

// --- Publish Version (draft → active) ---

public record PublishVersionCommand(Guid VersionId) : IRequest<HealthTestVersionDto?>;

public sealed class PublishVersionCommandHandler(IHealthTestRepository repository)
    : IRequestHandler<PublishVersionCommand, HealthTestVersionDto?>
{
    public async Task<HealthTestVersionDto?> Handle(
        PublishVersionCommand request,
        CancellationToken ct
    )
    {
        var version = await repository.GetVersionWithDetailsAsync(request.VersionId, ct);
        if (version is null)
        {
            return null;
        }

        if (version.Status != HealthTestVersionStatus.draft)
        {
            throw new BusinessRuleViolationException("Solo se publica una versión en estado Draft.");
        }

        var activeQuestions = version.Questions.Where(q => q.IsActive).ToList();
        if (activeQuestions.Count == 0) throw new BusinessRuleViolationException("Agrega preguntas antes de publicar.");
        if (activeQuestions.Any(q => (q.Type == HealthTestQuestionType.single || q.Type == HealthTestQuestionType.multi || q.Type == HealthTestQuestionType.scale) && !q.Options.Any(o => o.IsActive)))
            throw new BusinessRuleViolationException("Las preguntas de selección requieren opciones activas.");

        // El cambio de versión no deja un intervalo sin versión activa y serializa publicaciones concurrentes.
        var publishedAt = DateTime.UtcNow;
        if (!await repository.TryPublishVersionAsync(version.Id, version.InstrumentId, publishedAt, ct))
            throw new BusinessRuleViolationException("La versión cambió mientras publicabas. Actualiza e intenta de nuevo.");
        version.Status = HealthTestVersionStatus.active;
        version.IsCurrent = true;
        version.PublishedAt = publishedAt;

        return HealthTestVersionDto.FromEntity(version);
    }
}

// --- Retire Version (active → retired) ---

public record RetireVersionCommand(Guid VersionId) : IRequest<HealthTestVersionDto?>;

public sealed class RetireVersionCommandHandler(IHealthTestRepository repository)
    : IRequestHandler<RetireVersionCommand, HealthTestVersionDto?>
{
    public async Task<HealthTestVersionDto?> Handle(
        RetireVersionCommand request,
        CancellationToken ct
    )
    {
        var version = await repository.GetVersionByIdAsync(request.VersionId, ct);
        if (version is null)
        {
            return null;
        }

        version.Status = HealthTestVersionStatus.retired;
        version.IsCurrent = false;
        version.RetiredAt = DateTime.UtcNow;
        await repository.UpdateVersionAsync(version, ct);

        return HealthTestVersionDto.FromEntity(version);
    }
}

// --- Get Version (detalle con preguntas) ---

public record GetVersionQuery(Guid Id) : IRequest<HealthTestVersionDetailDto?>;

public sealed class GetVersionQueryHandler(IHealthTestRepository repository)
    : IRequestHandler<GetVersionQuery, HealthTestVersionDetailDto?>
{
    public async Task<HealthTestVersionDetailDto?> Handle(
        GetVersionQuery request,
        CancellationToken ct
    )
    {
        var version = await repository.GetVersionWithDetailsAsync(request.Id, ct);
        return version is null ? null : HealthTestVersionDetailDto.FromEntity(version);
    }
}

// --- Clone Version (crear N+1 copiando la versión origen) ---

public record CloneVersionCommand(Guid SourceVersionId) : IRequest<HealthTestVersionDto?>;

public sealed class CloneVersionCommandHandler(IHealthTestRepository repository)
    : IRequestHandler<CloneVersionCommand, HealthTestVersionDto?>
{
    public async Task<HealthTestVersionDto?> Handle(
        CloneVersionCommand request,
        CancellationToken ct
    )
    {
        var source = await repository.GetVersionWithDetailsAsync(request.SourceVersionId, ct);
        if (source is null)
        {
            return null;
        }

        var nextNumber = await repository.GetNextVersionNumberAsync(source.InstrumentId, ct);
        var clone = new HealthTestVersion
        {
            Id = Guid.NewGuid(),
            InstrumentId = source.InstrumentId,
            VersionNumber = nextNumber,
            Name = source.Name is null ? $"v{nextNumber}" : $"{source.Name} (copia v{nextNumber})",
            Status = HealthTestVersionStatus.draft,
            IsCurrent = false,
            ScoringStrategy = source.ScoringStrategy,
            Points = source.Points,
            CreatedAt = DateTime.UtcNow,
        };
        await repository.AddVersionAsync(clone, ct);

        // Remapea también las dependencias al nuevo borrador.
        var questionIds = source.Questions.ToDictionary(q => q.Id, _ => Guid.NewGuid());
        var optionIds = source.Questions.SelectMany(q => q.Options).ToDictionary(o => o.Id, _ => Guid.NewGuid());
        // Copia preguntas y opciones (con nuevos Ids).
        var questions = source
            .Questions.OrderBy(q => q.SortOrder)
            .Select(q =>
            {
                var newQ = new HealthTestQuestion
                {
                    Id = questionIds[q.Id],
                    VersionId = clone.Id,
                    Code = q.Code,
                    Section = q.Section,
                    Text = q.Text,
                    Type = q.Type,
                    ScoringDirection = q.ScoringDirection,
                    Unit = q.Unit, MinValue = q.MinValue, MaxValue = q.MaxValue, DefaultValue = q.DefaultValue,
                    MinLabel = q.MinLabel, MaxLabel = q.MaxLabel, Hint = q.Hint,
                    SortOrder = q.SortOrder,
                    IsActive = q.IsActive,
                };
                newQ.Options = q
                    .Options.OrderBy(o => o.SortOrder)
                    .Select(o => new HealthTestAnswerOption
                    {
                        Id = optionIds[o.Id],
                        QuestionId = newQ.Id,
                        Text = o.Text,
                        ScoreValue = o.ScoreValue,
                        DependsOnQuestionId = o.DependsOnQuestionId is {} qid && questionIds.TryGetValue(qid, out var newQid) ? newQid : null,
                        DependsOnOptionId = o.DependsOnOptionId is {} oid && optionIds.TryGetValue(oid, out var newOid) ? newOid : null,
                        SortOrder = o.SortOrder,
                        IsActive = o.IsActive,
                    })
                    .ToList();
                return newQ;
            })
            .ToList();
        await repository.AddQuestionsRangeAsync(questions, ct);

        var ranges = source
            .ScoreRanges.Select(r => new HealthTestScoreRange
            {
                Id = Guid.NewGuid(),
                VersionId = clone.Id,
                MinValue = r.MinValue,
                MaxValue = r.MaxValue,
                Label = r.Label,
                Severity = r.Severity,
                IsActive = r.IsActive,
            })
            .ToList();
        await repository.AddRangesRangeAsync(ranges, ct);

        return HealthTestVersionDto.FromEntity(clone);
    }
}
