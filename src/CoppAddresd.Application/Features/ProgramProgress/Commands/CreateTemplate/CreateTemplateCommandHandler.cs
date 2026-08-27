using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.CreateTemplate;

/// <summary>
/// Crea la plantilla en <c>Draft</c> (versión 1) con su conjunto de días y la
/// devuelve con el detalle (misma forma que <c>GET /templates/{id}</c>). El
/// repositorio reemplaza las filas por día y traduce el código duplicado a
/// 409 <c>TEMPLATE_CODE_EXISTS</c>.
/// </summary>
public sealed class CreateTemplateCommandHandler(
    IProgramRepository repository,
    ILogger<CreateTemplateCommandHandler> logger) : IRequestHandler<CreateTemplateCommand, ProgramTemplateDto>
{
    public async Task<ProgramTemplateDto> Handle(CreateTemplateCommand request, CancellationToken ct)
    {
        var template = new ProgramTemplate
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            TotalWeeks = request.TotalWeeks,
            Status = TemplateStatus.Draft,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
        };

        var days = ProgramProgressTemplateMapper.ToEntities(request.Days);

        var created = await repository.UpsertTemplateAsync(template, days, request.ActorId, ct);

        var dto = await repository.GetTemplateAsync(created.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer la plantilla creada.");

        logger.LogInformation(
            "Program.CreateTemplate: template={TemplateId} code={Code} semanas={TotalWeeks} filas={DayCount}",
            created.Id, created.Code, created.TotalWeeks, days.Count);

        return ProgramTemplateDto.FromEntity(dto);
    }
}