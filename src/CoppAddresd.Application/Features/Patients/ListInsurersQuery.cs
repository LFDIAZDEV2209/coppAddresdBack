using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>Lista el catálogo de aseguradoras (para filtros y formularios).</summary>
public record ListInsurersQuery : IRequest<IReadOnlyList<InsurerDto>>;

public sealed class ListInsurersQueryHandler(
    IPatientRepository repository) : IRequestHandler<ListInsurersQuery, IReadOnlyList<InsurerDto>>
{
    public async Task<IReadOnlyList<InsurerDto>> Handle(ListInsurersQuery request, CancellationToken ct)
    {
        var insurers = await repository.ListInsurersAsync(ct);
        return insurers.Select(InsurerDto.FromEntity).ToList();
    }
}