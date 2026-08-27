using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.ListXpRules;

/// <summary>
/// Consulta del catálogo completo de reglas XP (SPEC §14.5, ERP, permiso
/// <c>Program.Edit</c> en la API). El catálogo es pequeño y estable (11 reglas
/// sembradas), por lo que se devuelve completo ordenado por <c>code</c>, sin
/// paginación.
/// </summary>
public sealed record ListXpRulesQuery : IRequest<IReadOnlyList<XpRuleDto>>;

/// <summary>Mapea las entidades del catálogo al wire shape <see cref="XpRuleDto"/>.</summary>
public sealed class ListXpRulesQueryHandler(
    IXpRuleCatalogRepository repository) : IRequestHandler<ListXpRulesQuery, IReadOnlyList<XpRuleDto>>
{
    public async Task<IReadOnlyList<XpRuleDto>> Handle(ListXpRulesQuery request, CancellationToken ct)
    {
        var rules = await repository.ListAsync(ct);
        return rules.Select(XpRuleDto.FromEntity).ToList();
    }
}