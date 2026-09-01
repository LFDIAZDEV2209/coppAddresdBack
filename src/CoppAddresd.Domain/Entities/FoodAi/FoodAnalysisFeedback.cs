namespace CoppAddresd.Domain.Entities.FoodAi;

/// <summary>
/// Corrección del usuario sobre un análisis (schema foodai). Conserva el
/// valor ORIGINAL y el CORREGIDO (nunca sobrescribe): la comparación
/// modelo-original vs humano-corregido alimentará el dataset de entrenamiento
/// (FASE 10-12). Tipos: FOOD_WRONG, PORTION_WRONG, DETECTION_WRONG,
/// MISSING_FOOD, OTHER.
/// </summary>
public sealed class FoodAnalysisFeedback
{
    public Guid Id { get; set; }
    public Guid AnalysisId { get; set; }

    /// <summary>Item corregido (null = feedback del análisis completo).</summary>
    public Guid? ItemId { get; set; }

    /// <summary>Usuario de CoppAddresd que corrigió (auth.users).</summary>
    public Guid UserId { get; set; }

    public string FeedbackType { get; set; } = default!;

    // Corrección de alimento
    public string? OriginalFood { get; set; }
    public string? CorrectedFood { get; set; }

    // Corrección de porción
    public int? OriginalGrams { get; set; }
    public int? CorrectedGrams { get; set; }

    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public FoodAnalysis Analysis { get; set; } = default!;
}