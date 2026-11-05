using MediatR;

namespace CoppAddresd.Application.Features.Nutrition.Cases;

public sealed class NutritionCaseCommandHandler(NutritionCases cases) : IRequestHandler<NutritionCaseCommand, NutritionView>
{
    public Task<NutritionView> Handle(NutritionCaseCommand request, CancellationToken ct) => cases.ExecuteAsync(request, ct);
}
public sealed class GetNutritionCaseHandler(NutritionCases cases) : IRequestHandler<GetNutritionCaseQuery, NutritionView>
{
    public Task<NutritionView> Handle(GetNutritionCaseQuery request, CancellationToken ct) => cases.GetAsync(request, ct);
}
public sealed class ListNutritionCasesHandler(NutritionCases cases) : IRequestHandler<ListNutritionCasesQuery, IReadOnlyList<NutritionView>>
{
    public Task<IReadOnlyList<NutritionView>> Handle(ListNutritionCasesQuery request, CancellationToken ct) => cases.ListAsync(request, ct);
}
public sealed class NutritionHistoryHandler(NutritionCases cases) : IRequestHandler<NutritionHistoryQuery, IReadOnlyList<CoppAddresd.Domain.Entities.NutritionPrescriptionEvent>>
{
    public Task<IReadOnlyList<CoppAddresd.Domain.Entities.NutritionPrescriptionEvent>> Handle(NutritionHistoryQuery request, CancellationToken ct) => cases.HistoryAsync(request, ct);
}
