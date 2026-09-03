using CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetBaselines;

/// <summary>
/// Consulta de las líneas base clínicas de la inscripción (ERP, TASK-05):
/// resuelve el paciente de la inscripción y lista sus líneas base (SPEC
/// §13.1.2). Devuelve null si la inscripción no existe (404).
/// </summary>
public sealed record GetBaselinesQuery(Guid EnrollmentId) : IRequest<IReadOnlyList<ClinicalBaselineDto>?>;

/// <summary>Resuelve la inscripción (404 si no existe) y delega el listado.</summary>
public sealed class GetBaselinesHandler(
    IProgramRepository programRepository) : IRequestHandler<GetBaselinesQuery, IReadOnlyList<ClinicalBaselineDto>?>
{
    public async Task<IReadOnlyList<ClinicalBaselineDto>?> Handle(
        GetBaselinesQuery request, CancellationToken ct)
    {
        var enrollment = await programRepository.GetEnrollmentAsync(request.EnrollmentId, ct);
        if (enrollment is null)
        {
            return null;
        }

        return await programRepository.ListClinicalBaselinesAsync(enrollment.PatientId, ct);
    }
}