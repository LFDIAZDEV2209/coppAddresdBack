using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Acceso a las reglas de seguridad clínica activas
/// (<see cref="PlanSafetyRule"/>) para la evaluación de restricciones. Son
/// pocas reglas: una única query por evaluación (sin N+1).
/// </summary>
public interface ISafetyRuleRepository
{
    Task<IReadOnlyList<PlanSafetyRule>> GetActiveAsync(CancellationToken ct = default);
}