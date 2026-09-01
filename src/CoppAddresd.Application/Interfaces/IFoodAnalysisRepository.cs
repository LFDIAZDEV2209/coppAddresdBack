using CoppAddresd.Domain.Entities.FoodAi;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio de análisis de comida. Idempotente por AnalysisId: guardar un
/// análisis cuyo AnalysisId ya existe NO duplica (devuelve el existente).
/// </summary>
public interface IFoodAnalysisRepository
{
    Task<FoodAnalysis?> GetByAnalysisIdAsync(Guid analysisId, CancellationToken ct = default);

    /// <summary>
    /// Persiste el análisis con sus items. Si ya existe un análisis con el
    /// mismo AnalysisId, devuelve el existente sin duplicar.
    /// </summary>
    Task<FoodAnalysis> AddAsync(FoodAnalysis analysis, CancellationToken ct = default);

    Task AddFeedbackAsync(FoodAnalysisFeedback feedback, CancellationToken ct = default);
}