using CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.CreateBaseline;

/// <summary>
/// Command para registrar una línea base clínica desde el ERP (TASK-05):
/// resuelve el paciente de la inscripción y delega la escritura a
/// <see cref="IProgramRepository.UpsertClinicalBaselineAsync"/> — que
/// enforcea la guardia clínica AC-22 (403 sin rol clínico), valida
/// <c>value</c>/<c>targetValue</c> y hace UPSERT por
/// <c>(patient_id, metric_id)</c> (SPEC §13.1.2).
/// </summary>
public sealed record CreateBaselineCommand(
    Guid EnrollmentId,
    Guid MetricId,
    decimal Value,
    Guid UnitId,
    FavorableDirection FavorableDirection,
    DateOnly MeasuredAt,
    decimal? TargetValue,
    Guid ActorId,
    IReadOnlyList<string> CallerRoles) : IRequest<ClinicalBaselineDto>;

/// <summary>Resuelve la inscripción (404 si no existe) y delega el UPSERT.</summary>
public sealed class CreateBaselineHandler(
    IProgramRepository programRepository) : IRequestHandler<CreateBaselineCommand, ClinicalBaselineDto>
{
    public async Task<ClinicalBaselineDto> Handle(CreateBaselineCommand request, CancellationToken ct)
    {
        var enrollment = await programRepository.GetEnrollmentAsync(request.EnrollmentId, ct)
            ?? throw new NotFoundException($"Inscripción {request.EnrollmentId} no encontrada.");

        var write = new ClinicalBaselineWrite(
            PatientId: enrollment.PatientId,
            MetricId: request.MetricId,
            Value: request.Value,
            UnitId: request.UnitId,
            FavorableDirection: request.FavorableDirection,
            MeasuredAt: request.MeasuredAt,
            SetBy: request.ActorId,
            TargetValue: request.TargetValue);

        return await programRepository.UpsertClinicalBaselineAsync(write, request.CallerRoles, ct);
    }
}