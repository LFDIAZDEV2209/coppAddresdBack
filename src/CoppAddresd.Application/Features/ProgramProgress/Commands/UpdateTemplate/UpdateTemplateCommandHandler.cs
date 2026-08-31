using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateTemplate;

/// <summary>
/// Actualiza los metadatos editables de la plantilla (código, nombre,
/// descripción, semanas) y reemplaza su conjunto de días (SPEC §7.6). El
/// estado del ciclo de vida se preserva; la versión no cambia (solo publish la
/// incrementa). Si la plantilla no existe → 404.
/// </summary>
public sealed class UpdateTemplateCommandHandler(
    IProgramRepository repository,
    ILogger<UpdateTemplateCommandHandler> logger) : IRequestHandler<UpdateTemplateCommand, ProgramTemplateDto>
{
    public async Task<ProgramTemplateDto> Handle(UpdateTemplateCommand request, CancellationToken ct)
    {
        var existing = await repository.GetTemplateAsync(request.Id, ct)
            ?? throw new NotFoundException($"Plantilla {request.Id} no encontrada.");

        existing.Code = request.Code.Trim();
        existing.Name = request.Name.Trim();
        existing.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        existing.TotalWeeks = request.TotalWeeks;
        // Status/Version/PublishedAt intencionalmente intactos: el ciclo de
        // vida lo mueven Publish/Archive (SPEC §7.6).

        var days = ProgramProgressTemplateMapper.ToEntities(request.Days);
        var updated = await repository.UpsertTemplateAsync(existing, days, request.ActorId, ct);

        var dto = await repository.GetTemplateAsync(updated.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer la plantilla actualizada.");

        logger.LogInformation(
            "Program.UpdateTemplate: template={TemplateId} code={Code} version={Version} filas={DayCount}",
            updated.Id, updated.Code, updated.Version, days.Count);

        return ProgramTemplateDto.FromEntity(dto);
    }
}