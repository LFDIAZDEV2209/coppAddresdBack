using CoppAddresd.Application.DTOs.Ai;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Evalúa las reglas de seguridad clínicas activas contra el contexto
/// consolidado del paciente y devuelve las restricciones aplicables
/// (severidad <c>block</c> | <c>warning</c>) para condicionar el plan de la IA.
/// </summary>
public interface ISafetyRulesService
{
    Task<IReadOnlyList<RestrictionDto>> EvaluateAsync(ClinicalContextDto context, CancellationToken ct = default);
}