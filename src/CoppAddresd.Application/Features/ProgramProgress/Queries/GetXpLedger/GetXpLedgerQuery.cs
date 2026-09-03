using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetXpLedger;

/// <summary>
/// Consulta del historial de XP (libro mayor) de una inscripción (ERP,
/// TASK-04): paginada y ordenada descendente por fecha de otorgamiento.
/// Devuelve null si la inscripción no existe (el controller responde 404).
/// </summary>
public sealed record GetXpLedgerQuery(
    Guid EnrollmentId,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedXpLedgerResult?>;

/// <summary>
/// Orquesta la lectura del libro mayor: valida la existencia de la
/// inscripción (404 si no existe) y pagina las entradas del repositorio.
/// </summary>
public sealed class GetXpLedgerHandler(
    IProgramRepository programRepository)
    : IRequestHandler<GetXpLedgerQuery, PaginatedXpLedgerResult?>
{
    public async Task<PaginatedXpLedgerResult?> Handle(
        GetXpLedgerQuery request, CancellationToken ct)
    {
        var enrollment = await programRepository.GetEnrollmentAsync(request.EnrollmentId, ct);
        if (enrollment is null)
        {
            return null;
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (entries, total) = await programRepository
            .GetXpLedgerPageAsync(request.EnrollmentId, page, pageSize, ct);

        var dtos = entries
            .Select(e => new XpLedgerEntryDto(
                e.Id,
                e.EnrollmentId,
                e.Amount,
                e.Reason.ToString(),
                e.SourceRefType,
                e.SourceRefId,
                e.RuleCode,
                e.BalanceAfter,
                e.AwardedAt,
                e.GrantedBy,
                e.ValidatedBy,
                e.ValidatedAt,
                e.MultiplierUsed))
            .ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedXpLedgerResult(dtos, total, page, pageSize, totalPages);
    }
}