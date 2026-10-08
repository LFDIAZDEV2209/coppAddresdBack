using CoppAddresd.Application.Interfaces;
using MediatR;
namespace CoppAddresd.Application.Features.HealthTests.Catalog;
public record ListAlertRulesQuery : IRequest<IReadOnlyList<HealthTestAlertRuleCatalogDto>>;
public record HealthTestAlertRuleCatalogDto(Guid Id, string Name, string Condition, string Severity);
public sealed class ListAlertRulesQueryHandler(IHealthTestRepository repository) : IRequestHandler<ListAlertRulesQuery, IReadOnlyList<HealthTestAlertRuleCatalogDto>>
{
    public async Task<IReadOnlyList<HealthTestAlertRuleCatalogDto>> Handle(ListAlertRulesQuery request, CancellationToken ct)
        => (await repository.ListActiveAlertRulesAsync(ct)).Select(r => new HealthTestAlertRuleCatalogDto(r.Id, r.Name, r.Condition, r.Severity.ToString())).ToList();
}
