namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.ClinicalXp;

/// <summary>
/// Resultado de la evaluación de XP clínica tras un recálculo de puntajes
/// (SPEC §15, C): se dispara SOLO en <c>POST /scores/calculate</c> (nunca en
/// <c>GET /scores</c>). Resumen de lo producido para el log del handler:
/// cuántas revisiones significativas quedaron <c>pending</c> (requieren
/// decisión clínica, no otorgan XP aún), la XP auto-otorgada en el período
/// (dedupe parcial de <c>xp_ledger</c>) y qué reglas clínicas se otorgaron.
/// </summary>
public sealed record ClinicalXpEvaluationResult(
    int ReviewsCreated,
    int TotalXpAwarded,
    IReadOnlyList<string> AwardedRules)
{
    public static readonly ClinicalXpEvaluationResult Empty = new(0, 0, []);
}