namespace CoppAddresd.Domain.Entities.FoodAi;

/// <summary>
/// Alimento detectado dentro de un análisis (schema foodai). Guarda el
/// snapshot de detección + segmentación + porción + nutrición del momento.
/// La máscara se referencia por <see cref="MaskKey"/> (object storage), nunca
/// base64 en PostgreSQL.
/// </summary>
public sealed class FoodAnalysisItem
{
    public Guid Id { get; set; }
    public Guid AnalysisId { get; set; }

    /// <summary>Posición del alimento en el resultado del análisis (0-based).</summary>
    public int ItemIndex { get; set; }

    public string Name { get; set; } = default!;
    public double DetectionConfidence { get; set; }

    // === Bounding box (píxeles) ===
    public int BboxX { get; set; }
    public int BboxY { get; set; }
    public int BboxWidth { get; set; }
    public int BboxHeight { get; set; }

    // === Segmentación ===
    public string? MaskKey { get; set; }
    public int? MaskAreaPixels { get; set; }

    // === Porción (snapshot) ===
    public string? PortionSize { get; set; }
    public int? EstimatedGrams { get; set; }
    public int? MinGrams { get; set; }
    public int? MaxGrams { get; set; }
    public double? PortionConfidence { get; set; }
    public string? PortionMethod { get; set; }

    // === Nutrición (snapshot por alimento) ===
    public string? NutritionStatus { get; set; }
    public decimal? Calories { get; set; }
    public decimal? Protein { get; set; }
    public decimal? Carbohydrates { get; set; }
    public decimal? Fat { get; set; }
    public decimal? Fiber { get; set; }
    public decimal? Sugar { get; set; }
    public decimal? Sodium { get; set; }
    public string? Source { get; set; }
    public string? SourceVersion { get; set; }

    public FoodAnalysis Analysis { get; set; } = default!;
}