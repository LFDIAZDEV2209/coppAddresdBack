using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Wellness;

/// <summary>
/// Comando de generación de un plan (alimentación o ejercicio) con IA,
/// condicionado por el contexto clínico consolidado del paciente y las reglas
/// de seguridad activas. El plan devuelto llega listo para el formulario del
/// frontend (mismo shape que los requests de creación del módulo).
/// </summary>
public sealed record GeneratePlanCommand(Guid PatientId, string Type) : IRequest<GeneratePlanResponse>;

public sealed class GeneratePlanCommandHandler(
    IClinicalContextService clinicalContext,
    ISafetyRulesService safetyRules,
    IAiServiceClient aiService,
    ILogger<GeneratePlanCommandHandler> logger) : IRequestHandler<GeneratePlanCommand, GeneratePlanResponse>
{
    public async Task<GeneratePlanResponse> Handle(GeneratePlanCommand request, CancellationToken ct)
    {
        logger.LogInformation("Generando plan {Type} para el paciente {PatientId}",
            request.Type, request.PatientId);

        var context = await clinicalContext.ConsolidateAsync(request.PatientId, ct);
        var restrictions = await safetyRules.EvaluateAsync(context, ct);

        logger.LogInformation(
            "Contexto consolidado para {PatientId}: {MeasurementCount} mediciones, {RestrictionCount} restricciones",
            request.PatientId, context.Measurements.Count, restrictions.Count);

        var aiResult = await aiService.GeneratePlanAsync(request.Type, context, restrictions, ct);

        return new GeneratePlanResponse(request.Type, DateTime.UtcNow, aiResult.Plan);
    }
}