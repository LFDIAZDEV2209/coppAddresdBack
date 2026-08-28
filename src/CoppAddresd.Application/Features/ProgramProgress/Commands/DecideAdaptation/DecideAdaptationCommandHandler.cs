using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.DecideAdaptation;

/// <summary>
/// Orquesta la transición de la máquina de estados de una recomendación
/// (SPEC §5.6): delega en <c>DecideAdaptationAsync</c>, que persiste la
/// transición y —cuando el resultado queda <c>Applied</c>— registra la fila
/// semántica <c>action = 'AdaptationApplied'</c> en <c>audit.activity_logs</c>
/// (SPEC §6.8, AC-17) DENTRO de la misma transacción (todo-o-nada). El handler
/// queda delgado: no audita post-commit, así una fila de auditoría nunca queda
/// huérfana ni una transición aplicada sin auditoría.
/// La nota del clínico se loguea (sin PII) y queda para B5 como detalle de la
/// decisión; la tabla de adaptaciones no tiene columna de nota en MVP.
/// </summary>
public sealed class DecideAdaptationCommandHandler(
    IProgramRepository repository,
    ILogger<DecideAdaptationCommandHandler> logger) : IRequestHandler<DecideAdaptationCommand, AdaptationRecommendationDto>
{
    public async Task<AdaptationRecommendationDto> Handle(DecideAdaptationCommand request, CancellationToken ct)
    {
        var adaptation = await repository.DecideAdaptationAsync(
            request.AdaptationId, request.Decision, request.ActorId,
            auditActionOnApply: "AdaptationApplied", ct);

        logger.LogInformation(
            "Program.AdaptationApplied: adaptation={AdaptationId} decision={Decision} " +
            "estado={Status} enrollment={EnrollmentId}",
            adaptation.Id, request.Decision, adaptation.Status, adaptation.EnrollmentId);

        return AdaptationRecommendationDto.FromEntity(adaptation);
    }
}