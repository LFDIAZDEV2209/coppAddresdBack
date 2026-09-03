using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.Erp;

/// <summary>Listado paginado de pacientes con indicadores de Biometría.</summary>
public sealed record ListBiometriaPatientsQuery(
    string? Search,
    string? Gender,
    string? ImcCategory,
    string? GlucosaCategory,
    string? GrasaCategory,
    string? Trend,
    Guid? CityId,
    string? StateAbbr,
    int Page,
    int PageSize) : IRequest<(IReadOnlyList<BiometriaPatientListItemDto> Items, int Total)>;

public sealed class ListBiometriaPatientsQueryHandler(IProgramRepository repository)
    : IRequestHandler<ListBiometriaPatientsQuery, (IReadOnlyList<BiometriaPatientListItemDto> Items, int Total)>
{
    public Task<(IReadOnlyList<BiometriaPatientListItemDto> Items, int Total)> Handle(
        ListBiometriaPatientsQuery request, CancellationToken ct)
        => repository.ListBiometriaPatientsAsync(
            request.Search, request.Gender, request.ImcCategory, request.GlucosaCategory,
            request.GrasaCategory, request.Trend,
            request.CityId, request.StateAbbr,
            request.Page, request.PageSize, ct);
}
