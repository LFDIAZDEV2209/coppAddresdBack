using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using MediatR;

namespace CoppAddresd.Application.Features.HealthTests.Notifications;

// ─────────────────────────────────────────────────────────────────────────────
// Consultas de plantillas
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Lista paginada de plantillas de notificación (Template Studio).</summary>
public record ListNotificationTemplatesQuery(
    NotificationChannel? Channel = null,
    string? Search = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<PaginatedHealthTestsResult<HealthTestNotificationTemplateDto>>;

public sealed class ListNotificationTemplatesQueryHandler(
    IHealthTestNotificationRepository repository
)
    : IRequestHandler<
        ListNotificationTemplatesQuery,
        PaginatedHealthTestsResult<HealthTestNotificationTemplateDto>
    >
{
    public async Task<PaginatedHealthTestsResult<HealthTestNotificationTemplateDto>> Handle(
        ListNotificationTemplatesQuery request,
        CancellationToken ct
    )
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var templates = await repository.ListTemplatesAsync(
            request.Channel,
            request.Search,
            request.IsActive,
            page,
            pageSize,
            ct
        );
        var total = await repository.CountTemplatesAsync(
            request.Channel,
            request.Search,
            request.IsActive,
            ct
        );
        var usage = await repository.GetTemplateUsageCountsAsync(ct);

        var items = templates
            .Select(t =>
                HealthTestNotificationTemplateDto.FromEntity(
                    t,
                    usage.TryGetValue(t.Id, out var count) ? count : 0,
                    t.Versions.Count
                )
            )
            .ToList();

        return new PaginatedHealthTestsResult<HealthTestNotificationTemplateDto>(
            items,
            total,
            page,
            pageSize,
            (int)Math.Ceiling(total / (double)pageSize)
        );
    }
}

/// <summary>Detalle de una plantilla, opcionalmente con su historial de versiones.</summary>
public record GetNotificationTemplateQuery(Guid Id, bool IncludeVersions = false)
    : IRequest<HealthTestNotificationTemplateDto?>;

public sealed class GetNotificationTemplateQueryHandler(
    IHealthTestNotificationRepository repository
) : IRequestHandler<GetNotificationTemplateQuery, HealthTestNotificationTemplateDto?>
{
    public async Task<HealthTestNotificationTemplateDto?> Handle(
        GetNotificationTemplateQuery request,
        CancellationToken ct
    )
    {
        var template = await repository.GetTemplateByIdAsync(request.Id, true, ct);
        if (template is null)
        {
            return null;
        }

        var usage = await repository.GetTemplateUsageCountsAsync(ct);
        return HealthTestNotificationTemplateDto.FromEntity(
            template,
            usage.TryGetValue(template.Id, out var count) ? count : 0,
            template.Versions.Count
        );
    }
}

/// <summary>Historial de versiones de una plantilla.</summary>
public record ListNotificationTemplateVersionsQuery(Guid TemplateId)
    : IRequest<IReadOnlyList<HealthTestNotificationTemplateVersionDto>>;

public sealed class ListNotificationTemplateVersionsQueryHandler(
    IHealthTestNotificationRepository repository
)
    : IRequestHandler<
        ListNotificationTemplateVersionsQuery,
        IReadOnlyList<HealthTestNotificationTemplateVersionDto>
    >
{
    public async Task<IReadOnlyList<HealthTestNotificationTemplateVersionDto>> Handle(
        ListNotificationTemplateVersionsQuery request,
        CancellationToken ct
    )
    {
        var versions = await repository.ListTemplateVersionsAsync(request.TemplateId, ct);
        return versions.Select(HealthTestNotificationTemplateVersionDto.FromEntity).ToList();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Comandos de plantillas
// ─────────────────────────────────────────────────────────────────────────────

public record CreateNotificationTemplateCommand(
    CreateNotificationTemplateRequest Request,
    Guid? ActorId = null
) : IRequest<HealthTestNotificationTemplateDto?>;

public sealed class CreateNotificationTemplateCommandHandler(
    IHealthTestNotificationRepository repository
) : IRequestHandler<CreateNotificationTemplateCommand, HealthTestNotificationTemplateDto?>
{
    public async Task<HealthTestNotificationTemplateDto?> Handle(
        CreateNotificationTemplateCommand command,
        CancellationToken ct
    )
    {
        var request = command.Request;
        var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.BodyTemplate))
        {
            return null;
        }

        var existing = await repository.GetTemplateByCodeAsync(code, ct);
        if (existing is not null)
        {
            return null;
        }

        var template = new HealthTestNotificationTemplate
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            Channel = request.Channel,
            Severity = request.Severity,
            TestCategory = string.IsNullOrWhiteSpace(request.TestCategory)
                ? null
                : request.TestCategory.Trim(),
            IndicatorCode = string.IsNullOrWhiteSpace(request.IndicatorCode)
                ? null
                : request.IndicatorCode.Trim(),
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim(),
            BodyTemplate = request.BodyTemplate.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
        };

        await repository.AddTemplateAsync(template, ct);
        await repository.AddTemplateVersionAsync(
            Snapshot(template, 1, request.Note, command.ActorId),
            ct
        );

        return HealthTestNotificationTemplateDto.FromEntity(template, 0, 1);
    }

    private static HealthTestNotificationTemplateVersion Snapshot(
        HealthTestNotificationTemplate template,
        int version,
        string? note,
        Guid? actorId
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            TemplateId = template.Id,
            Version = version,
            Name = template.Name,
            Channel = template.Channel,
            Severity = template.Severity,
            TestCategory = template.TestCategory,
            IndicatorCode = template.IndicatorCode,
            Subject = template.Subject,
            BodyTemplate = template.BodyTemplate,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow,
        };
}

public record UpdateNotificationTemplateCommand(
    Guid Id,
    UpdateNotificationTemplateRequest Request,
    Guid? ActorId = null
) : IRequest<HealthTestNotificationTemplateDto?>;

public sealed class UpdateNotificationTemplateCommandHandler(
    IHealthTestNotificationRepository repository
) : IRequestHandler<UpdateNotificationTemplateCommand, HealthTestNotificationTemplateDto?>
{
    public async Task<HealthTestNotificationTemplateDto?> Handle(
        UpdateNotificationTemplateCommand command,
        CancellationToken ct
    )
    {
        var template = await repository.GetTemplateByIdAsync(command.Id, true, ct);
        if (template is null)
        {
            return null;
        }

        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.BodyTemplate))
        {
            return null;
        }

        var contentChanged =
            template.BodyTemplate != request.BodyTemplate.Trim()
            || template.Name != request.Name.Trim()
            || template.Channel != request.Channel
            || template.Subject != (request.Subject?.Trim())
            || template.Severity != request.Severity
            || template.TestCategory != (request.TestCategory?.Trim())
            || template.IndicatorCode != (request.IndicatorCode?.Trim());

        template.Name = request.Name.Trim();
        template.Channel = request.Channel;
        template.Severity = request.Severity;
        template.TestCategory = string.IsNullOrWhiteSpace(request.TestCategory)
            ? null
            : request.TestCategory.Trim();
        template.IndicatorCode = string.IsNullOrWhiteSpace(request.IndicatorCode)
            ? null
            : request.IndicatorCode.Trim();
        template.Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim();
        template.BodyTemplate = request.BodyTemplate.Trim();
        template.IsActive = request.IsActive;
        template.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateTemplateAsync(template, ct);

        if (contentChanged)
        {
            var next = await repository.GetNextTemplateVersionNumberAsync(template.Id, ct);
            await repository.AddTemplateVersionAsync(
                Snapshot(template, next, request.Note, command.ActorId),
                ct
            );
            template.Versions.Add(
                Snapshot(template, next, request.Note, command.ActorId)
            );
        }

        return HealthTestNotificationTemplateDto.FromEntity(template, 0, template.Versions.Count);
    }

    private static HealthTestNotificationTemplateVersion Snapshot(
        HealthTestNotificationTemplate template,
        int version,
        string? note,
        Guid? actorId
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            TemplateId = template.Id,
            Version = version,
            Name = template.Name,
            Channel = template.Channel,
            Severity = template.Severity,
            TestCategory = template.TestCategory,
            IndicatorCode = template.IndicatorCode,
            Subject = template.Subject,
            BodyTemplate = template.BodyTemplate,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow,
        };
}

/// <summary>Activa o desactiva una plantilla (borrado lógico vía DELETE).</summary>
public record SetNotificationTemplateActiveCommand(
    Guid Id,
    bool IsActive,
    Guid? ActorId = null
) : IRequest<HealthTestNotificationTemplateDto?>;

public sealed class SetNotificationTemplateActiveCommandHandler(
    IHealthTestNotificationRepository repository
) : IRequestHandler<SetNotificationTemplateActiveCommand, HealthTestNotificationTemplateDto?>
{
    public async Task<HealthTestNotificationTemplateDto?> Handle(
        SetNotificationTemplateActiveCommand command,
        CancellationToken ct
    )
    {
        var template = await repository.GetTemplateByIdAsync(command.Id, false, ct);
        if (template is null)
        {
            return null;
        }

        template.IsActive = command.IsActive;
        template.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateTemplateAsync(template, ct);

        var usage = await repository.GetTemplateUsageCountsAsync(ct);
        return HealthTestNotificationTemplateDto.FromEntity(
            template,
            usage.TryGetValue(template.Id, out var count) ? count : 0,
            0
        );
    }
}

public record CloneNotificationTemplateCommand(
    Guid Id,
    CloneNotificationTemplateRequest Request,
    Guid? ActorId = null
) : IRequest<HealthTestNotificationTemplateDto?>;

public sealed class CloneNotificationTemplateCommandHandler(
    IHealthTestNotificationRepository repository
) : IRequestHandler<CloneNotificationTemplateCommand, HealthTestNotificationTemplateDto?>
{
    public async Task<HealthTestNotificationTemplateDto?> Handle(
        CloneNotificationTemplateCommand command,
        CancellationToken ct
    )
    {
        var source = await repository.GetTemplateByIdAsync(command.Id, false, ct);
        if (source is null)
        {
            return null;
        }

        var code = (command.Request.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var existing = await repository.GetTemplateByCodeAsync(code, ct);
        if (existing is not null)
        {
            return null;
        }

        var clone = new HealthTestNotificationTemplate
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = string.IsNullOrWhiteSpace(command.Request.Name)
                ? $"{source.Name} (copia)"
                : command.Request.Name!.Trim(),
            Channel = source.Channel,
            Severity = source.Severity,
            TestCategory = source.TestCategory,
            IndicatorCode = source.IndicatorCode,
            Subject = source.Subject,
            BodyTemplate = source.BodyTemplate,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        };

        await repository.AddTemplateAsync(clone, ct);
        await repository.AddTemplateVersionAsync(
            new HealthTestNotificationTemplateVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = clone.Id,
                Version = 1,
                Name = clone.Name,
                Channel = clone.Channel,
                Severity = clone.Severity,
                TestCategory = clone.TestCategory,
                IndicatorCode = clone.IndicatorCode,
                Subject = clone.Subject,
                BodyTemplate = clone.BodyTemplate,
                Note = $"Clonada de {source.Code}",
                CreatedBy = command.ActorId,
                CreatedAt = DateTime.UtcNow,
            },
            ct
        );

        return HealthTestNotificationTemplateDto.FromEntity(clone, 0, 1);
    }
}

/// <summary>Restaura el contenido de una versión anterior como nueva versión vigente.</summary>
public record RestoreNotificationTemplateVersionCommand(
    Guid Id,
    int Version,
    Guid? ActorId = null
) : IRequest<HealthTestNotificationTemplateDto?>;

public sealed class RestoreNotificationTemplateVersionCommandHandler(
    IHealthTestNotificationRepository repository
) : IRequestHandler<RestoreNotificationTemplateVersionCommand, HealthTestNotificationTemplateDto?>
{
    public async Task<HealthTestNotificationTemplateDto?> Handle(
        RestoreNotificationTemplateVersionCommand command,
        CancellationToken ct
    )
    {
        var template = await repository.GetTemplateByIdAsync(command.Id, true, ct);
        if (template is null)
        {
            return null;
        }

        var version = await repository.GetTemplateVersionAsync(command.Id, command.Version, ct);
        if (version is null)
        {
            return null;
        }

        template.Name = version.Name;
        template.Channel = version.Channel;
        template.Severity = version.Severity;
        template.TestCategory = version.TestCategory;
        template.IndicatorCode = version.IndicatorCode;
        template.Subject = version.Subject;
        template.BodyTemplate = version.BodyTemplate;
        template.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateTemplateAsync(template, ct);

        var next = await repository.GetNextTemplateVersionNumberAsync(template.Id, ct);
        await repository.AddTemplateVersionAsync(
            new HealthTestNotificationTemplateVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                Version = next,
                Name = template.Name,
                Channel = template.Channel,
                Severity = template.Severity,
                TestCategory = template.TestCategory,
                IndicatorCode = template.IndicatorCode,
                Subject = template.Subject,
                BodyTemplate = template.BodyTemplate,
                Note = $"Restaurada desde v{version.Version}",
                CreatedBy = command.ActorId,
                CreatedAt = DateTime.UtcNow,
            },
            ct
        );

        return HealthTestNotificationTemplateDto.FromEntity(template, 0, next);
    }
}
