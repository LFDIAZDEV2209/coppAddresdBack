using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetTemplate;

/// <summary>
/// Consulta del detalle de una plantilla con sus filas por día ordenadas
/// (SPEC §7.6). Plantilla inexistente → 404.
/// </summary>
public sealed record GetTemplateQuery(
    Guid Id) : IRequest<ProgramTemplateDto>;

/// <summary>Lee la plantilla (con días) y la mapea al shape del detalle.</summary>
public sealed class GetTemplateQueryHandler(
    IProgramRepository repository) : IRequestHandler<GetTemplateQuery, ProgramTemplateDto>
{
    public async Task<ProgramTemplateDto> Handle(GetTemplateQuery request, CancellationToken ct)
    {
        var template = await repository.GetTemplateAsync(request.Id, ct)
            ?? throw new NotFoundException($"Plantilla {request.Id} no encontrada.");

        return ProgramTemplateDto.FromEntity(template);
    }
}