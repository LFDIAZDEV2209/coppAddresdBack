using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetSnapshot;

/// <summary>
/// Consulta del snapshot del programa para la home del móvil (SPEC §7.1).
/// <c>TodayLocalDate</c> es opcional: si no se envía, el handler computa el
/// "hoy" en la zona IANA del paciente (SPEC §6.11). Sin inscripción → 404
/// <c>NO_ACTIVE_ENROLLMENT</c>.
/// </summary>
public sealed record GetSnapshotQuery(
    Guid EnrollmentId,
    DateOnly? TodayLocalDate = null) : IRequest<ProgramSnapshotDto>;

/// <summary>Orquesta la lectura del snapshot (una sola proyección del repositorio, sin N+1).</summary>
public sealed class GetSnapshotQueryHandler(
    IProgramRepository repository) : IRequestHandler<GetSnapshotQuery, ProgramSnapshotDto>
{
    public async Task<ProgramSnapshotDto> Handle(GetSnapshotQuery request, CancellationToken ct)
    {
        var todayLocal = request.TodayLocalDate
            ?? await repository.GetPatientLocalTodayAsync(request.EnrollmentId, ct)
            ?? throw new NotFoundException(
                $"NO_ACTIVE_ENROLLMENT: no existe la inscripción {request.EnrollmentId}.");

        var snapshot = await repository.GetSnapshotAsync(request.EnrollmentId, todayLocal, ct)
            ?? throw new NotFoundException(
                $"NO_ACTIVE_ENROLLMENT: no existe la inscripción {request.EnrollmentId}.");

        return snapshot;
    }
}