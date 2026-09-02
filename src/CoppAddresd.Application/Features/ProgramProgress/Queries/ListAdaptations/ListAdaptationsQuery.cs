using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.ListAdaptations;

/// <summary>
/// Consulta paginada de recomendaciones de adaptación (SPEC §7.7, ERP, permiso
/// <c>Program.View</c> en la API). Filtros opcionales por inscripción y estado.
/// </summary>
public sealed record ListAdaptationsQuery(
    Guid? EnrollmentId = null,
    AdaptationStatus? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedAdaptationsResult>;

/// <summary>Página el listado y mapea las entidades al DTO (payload jsonb opaco).</summary>
public sealed class ListAdaptationsQueryHandler(
    IProgramRepository repository) : IRequestHandler<ListAdaptationsQuery, PaginatedAdaptationsResult>
{
    public async Task<PaginatedAdaptationsResult> Handle(ListAdaptationsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, total) = await repository.ListAdaptationsAsync(
            request.EnrollmentId, request.Status, page, pageSize, ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedAdaptationsResult(
            items.Select(a =>
            {
                var patientName = a.Enrollment?.Patient is null
                    ? null
                    : $"{a.Enrollment.Patient.FirstName} {a.Enrollment.Patient.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(patientName)) patientName = null;
                var dto = AdaptationRecommendationDto.FromEntity(a);
                return dto with { PatientName = patientName };
            }).ToList(),
            total, page, pageSize, totalPages);
    }
}