using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Interfaces;
using MediatR;
using System.Runtime.CompilerServices;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.Erp;

/// <summary>Stream de biometría para exporte CSV.</summary>
public sealed record StreamBiometriaPatientsExportQuery(
    string? Search = null, string? Gender = null, string? ImcCategory = null,
    string? GlucosaCategory = null, string? GrasaCategory = null, string? Trend = null,
    Guid? CityId = null, string? StateAbbr = null) : IStreamRequest<BiometriaPatientListItemDto>;

public sealed class StreamBiometriaPatientsExportQueryHandler(IProgramRepository repository)
    : IStreamRequestHandler<StreamBiometriaPatientsExportQuery, BiometriaPatientListItemDto>
{
    public async IAsyncEnumerable<BiometriaPatientListItemDto> Handle(
        StreamBiometriaPatientsExportQuery request, [EnumeratorCancellation] CancellationToken ct)
    {
        // El listado limita cada página a 100; int.MaxValue truncaba el CSV silenciosamente.
        const int pageSize = 100;
        for (var page = 1; ; page++)
        {
            ct.ThrowIfCancellationRequested();
            var (items, total) = await repository.ListBiometriaPatientsAsync(
                request.Search, request.Gender, request.ImcCategory, request.GlucosaCategory,
                request.GrasaCategory, request.Trend, request.CityId, request.StateAbbr,
                page, pageSize, ct);
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                yield return item;
            }
            if (items.Count == 0 || (long)page * pageSize >= total)
                yield break;
        }
    }
}
