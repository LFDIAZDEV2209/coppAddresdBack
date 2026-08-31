using CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetScores;

/// <summary>
/// Consulta de los puntajes del paciente autenticado (SPEC §13.7.1):
/// Índice de Salud + Índice de Transformación del período/semana actual.
/// Compute-on-read (SPEC §13.3): el repositorio recalcula y persiste la fila
/// del período si falta o está vencida, y devuelve el <c>previous</c>
/// persistido para la tendencia. Sin inscripción activa → 404
/// <c>NO_ACTIVE_ENROLLMENT</c>.
///
/// El <c>patientId</c> SIEMPRE llega resuelto de la identidad del JWT por la
/// capa API (nunca del body): es la base del anti-IDOR (AC-11) — un cruce
/// entre pacientes devuelve 404, nunca 403.
/// </summary>
public sealed record GetScoresQuery(Guid PatientId) : IRequest<ScoresResponseDto>;

/// <summary>Orquesta la lectura de ambos puntajes vía el repositorio (una sola proyección por puntaje, sin N+1).</summary>
public sealed class GetScoresQueryHandler(
    IProgramRepository repository) : IRequestHandler<GetScoresQuery, ScoresResponseDto>
{
    public async Task<ScoresResponseDto> Handle(GetScoresQuery request, CancellationToken ct)
    {
        var health = await repository.GetOrComputeHealthScoreAsync(
            request.PatientId, ScoreTrigger.OnRead, ct: ct)
            ?? throw new NotFoundException(
                $"NO_ACTIVE_ENROLLMENT: no existe una inscripción activa para el paciente {request.PatientId}.");

        var transformation = await repository.GetOrComputeTransformationScoreAsync(
            request.PatientId, ScoreTrigger.OnRead, ct: ct)
            ?? throw new NotFoundException(
                $"NO_ACTIVE_ENROLLMENT: no existe una inscripción activa para el paciente {request.PatientId}.");

        return new ScoresResponseDto(health, transformation);
    }
}