using CoppAddresd.Application.Features.ProgramProgress.DTOs.Weaknesses;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.ListWeaknesses;

/// <summary>
/// Debilidades del paciente autenticado (SPEC §21, D): listado paginado
/// (default 20, orden descendente por <c>detected_at</c>) de los hallazgos del
/// motor de detección + los registrados por profesionales. El <c>patientId</c>
/// SIEMPRE llega resuelto de la identidad del JWT por la capa API (nunca del
/// body): es la base del anti-IDOR (AC-11) — un cruce entre pacientes devuelve
/// 404, nunca 403.
/// </summary>
public sealed record ListWeaknessesQuery(Guid PatientId, int Page = 1, int PageSize = 20)
    : IRequest<PaginatedWeaknessesResult>;

/// <summary>Orquesta la lectura de las debilidades del paciente (proyección, sin N+1).</summary>
public sealed class ListWeaknessesQueryHandler(
    IProgramRepository repository) : IRequestHandler<ListWeaknessesQuery, PaginatedWeaknessesResult>
{
    public async Task<PaginatedWeaknessesResult> Handle(
        ListWeaknessesQuery request, CancellationToken ct)
    {
        var (items, total) = await repository.ListWeaknessesAsync(
            request.PatientId, request.Page, request.PageSize, ct);

        return new PaginatedWeaknessesResult(
            items, total, request.Page, request.PageSize,
            (int)Math.Ceiling(total / (double)Math.Max(1, request.PageSize)));
    }
}