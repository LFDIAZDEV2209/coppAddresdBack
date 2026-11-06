using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Nutrition.Cases;

public sealed class NutritionDraftWorkflow(INutritionDraftReservationStore reservations, IProgramAssignmentRepository assessments,
    NutritionCases cases, ILogger<NutritionDraftWorkflow> logger, TimeProvider clock) : INutritionDraftWorkflow
{
    public async Task<NutritionDraftReservation?> QueueAsync(Guid patientId, Guid batteryAssignmentId, CancellationToken ct)
    {
        if (await assessments.FindCompletedInitialBatteryAsync(patientId, batteryAssignmentId, ct) is null) return null;
        using var actor = NutritionSystemActor.Begin();
        return await reservations.ReserveAsync(patientId, batteryAssignmentId, ct);
    }

    public async Task ReconcileAsync(CancellationToken ct)
    {
        // Lote máximo de 25 baterías; no se carga contenido clínico ni menús por fila.
        // MissingAsync ya verifica completitud por conjuntos; no reconsulta el perfil por cada fila.
        using var actor = NutritionSystemActor.Begin();
        foreach (var row in await reservations.MissingAsync(ct)) await reservations.ReserveAsync(row.PatientId, row.BatteryId, ct);
    }

    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        using var actor = NutritionSystemActor.Begin();
        var r = await reservations.ClaimAsync(clock.GetUtcNow().UtcDateTime, ct);
        if (r is null) return false;
        try
        {
            var plan = await cases.ExecuteAsync(new(NutritionOperation.Generate, r.PatientId, null, null,
                NutritionSystemActor.Actor, CorrelationId: r.Id, BatteryAssignmentId: r.BatteryAssignmentId), ct);
            await reservations.CompleteAsync(r, plan.Id, plan.NarrativeSource == "deterministic_fallback", ct);
            logger.LogInformation("Nutrition.DraftReady ReservationId={ReservationId} Source={Source}", r.Id, plan.NarrativeSource);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // Solo se persiste el tipo de fallo; nunca datos clínicos, respuestas LLM ni credenciales.
            await reservations.FailAsync(r, ex.GetType().Name, clock.GetUtcNow().UtcDateTime, ct);
            logger.LogWarning("Nutrition.DraftFailed ReservationId={ReservationId} FailureType={FailureType}", r.Id, ex.GetType().Name);
        }
        return true;
    }
}
