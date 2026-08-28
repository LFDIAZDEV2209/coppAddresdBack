using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.ArchiveTemplate;

/// <summary>
/// Archiva la plantilla (404 si no existe; idempotente si ya está archivada).
/// Las inscripciones existentes siguen usando su snapshot (SPEC §4.5): archivar
/// no rompe semanas en curso.
/// </summary>
public sealed class ArchiveTemplateCommandHandler(
    IProgramRepository repository,
    ILogger<ArchiveTemplateCommandHandler> logger) : IRequestHandler<ArchiveTemplateCommand, ProgramTemplateDto>
{
    public async Task<ProgramTemplateDto> Handle(ArchiveTemplateCommand request, CancellationToken ct)
    {
        var existing = await repository.GetTemplateAsync(request.Id, ct)
            ?? throw new NotFoundException($"Plantilla {request.Id} no encontrada.");

        if (existing.Status != TemplateStatus.Archived)
        {
            existing.Status = TemplateStatus.Archived;
            var days = existing.DayTemplates.ToList();
            var archived = await repository.UpsertTemplateAsync(existing, days, request.ActorId, ct);
            existing = archived;
        }

        logger.LogInformation(
            "Program.ArchiveTemplate: template={TemplateId} code={Code}",
            existing.Id, existing.Code);

        return ProgramTemplateDto.FromEntity(existing);
    }
}