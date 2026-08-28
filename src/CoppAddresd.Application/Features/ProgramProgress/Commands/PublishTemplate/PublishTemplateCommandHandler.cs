using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.PublishTemplate;

/// <summary>
/// Publica la plantilla: carga el estado actual (404 si no existe), rechaza
/// las archivadas (409), incrementa la versión y fija <c>Active</c> +
/// <c>published_at</c> (SPEC §7.6). La semántica de "bump version" la aplica
/// este handler; el repositorio la persiste tal cual
/// (<c>UpsertTemplateAsync</c> no toca version por diseño).
/// </summary>
public sealed class PublishTemplateCommandHandler(
    IProgramRepository repository,
    ILogger<PublishTemplateCommandHandler> logger) : IRequestHandler<PublishTemplateCommand, ProgramTemplateDto>
{
    public async Task<ProgramTemplateDto> Handle(PublishTemplateCommand request, CancellationToken ct)
    {
        var existing = await repository.GetTemplateAsync(request.Id, ct)
            ?? throw new NotFoundException($"Plantilla {request.Id} no encontrada.");

        if (existing.Status == TemplateStatus.Archived)
        {
            throw new BusinessRuleViolationException(
                "TEMPLATE_STATE: no se puede publicar una plantilla archivada.");
        }

        existing.Version += 1;
        existing.Status = TemplateStatus.Active;
        existing.PublishedAt = DateTime.UtcNow;

        var days = existing.DayTemplates.ToList();
        var published = await repository.UpsertTemplateAsync(existing, days, request.ActorId, ct);

        var dto = await repository.GetTemplateAsync(published.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer la plantilla publicada.");

        logger.LogInformation(
            "Program.PublishTemplate: template={TemplateId} code={Code} version={Version}",
            published.Id, published.Code, published.Version);

        return ProgramTemplateDto.FromEntity(dto);
    }
}