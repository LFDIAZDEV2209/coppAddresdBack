using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.ReconcileStreaks;

/// <summary>
/// Resumen de una pasada de reconciliación de rachas (B12, T-28): inscripciones
/// activas evaluadas, filas de <c>streak_states</c> corregidas y el detalle de
/// las discrepancias encontradas (capado al logueo — puede crecer).
/// </summary>
public sealed record StreakReconciliationSummary(
    int Scanned,
    int Fixed,
    IReadOnlyList<string> Discrepancies);

/// <summary>
/// Disparo manual de la reconciliación nocturna de rachas (B12, T-28): recalcula
/// <c>streak_states</c> desde <c>task_completions</c> + congelamientos
/// consumidos por inscripción activa y corrige las filas desviadas. Requiere
/// <c>Program.Edit</c> en la API (mantenimiento); el job nocturno (hosted
/// service) llama al mismo camino sin pasar por aquí.
/// </summary>
public sealed record ReconcileStreaksCommand : IRequest<StreakReconciliationSummary>;

public sealed class ReconcileStreaksCommandHandler(
    ReconcileStreakJob job)
    : IRequestHandler<ReconcileStreaksCommand, StreakReconciliationSummary>
{
    public Task<StreakReconciliationSummary> Handle(
        ReconcileStreaksCommand request, CancellationToken ct)
        => job.RunAsync(ct);
}

/// <summary>
/// Job de reconciliación de rachas (B12, T-28): idempotente (re-correr no
/// vuelve a corregir), recorre las inscripciones activas y reconstruye
/// <c>current_streak</c>/<c>longest_streak</c>/<c>last_active_date</c> desde la
/// fuente de verdad (<c>task_completions</c> + congelamientos consumidos).
/// NUNCA toca <c>daily_checkins.is_perfect_day</c> (escrito por el handler del
/// día) ni el inventario de congelamientos.
/// </summary>
public sealed class ReconcileStreakJob(
    IProgramRepository repository,
    ILogger<ReconcileStreakJob> logger)
{
    public async Task<StreakReconciliationSummary> RunAsync(CancellationToken ct = default)
    {
        var summary = await repository.ReconcileStreaksAsync(ct);

        logger.LogInformation(
            "Program.StreakReconciliation: scanned={Scanned} fixed={Fixed}",
            summary.Scanned, summary.Fixed);

        // Log de discrepancias sin PHI (ids y números, convención T-41).
        foreach (var detail in summary.Discrepancies.Take(20))
        {
            logger.LogInformation(
                "Program.StreakDiscrepancy: {Detail}", detail);
        }

        return summary;
    }
}
