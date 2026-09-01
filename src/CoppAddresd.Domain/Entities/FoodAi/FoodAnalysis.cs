namespace CoppAddresd.Domain.Entities.FoodAi;

/// <summary>
/// Análisis de una imagen de comida (schema foodai). Persiste el resultado
/// completo del pipeline con snapshot nutricional y versiones de modelo para
/// que el análisis sea reconstruible aunque los modelos/DB cambien después.
/// La imagen original se referencia por <see cref="ImageKey"/> (object storage);
/// nunca se guardan blobs en PostgreSQL.
/// </summary>
public sealed class FoodAnalysis
{
    public Guid Id { get; set; }

    /// <summary>Identificador público del análisis (idempotencia: único).</summary>
    public Guid AnalysisId { get; set; }

    public string Status { get; set; } = "completed";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Clave del objeto (IObjectStorageService) de la imagen original.</summary>
    public string? ImageKey { get; set; }

    /// <summary>Usuario de CoppAddresd que creó el análisis (auth.users). Null si anónimo.</summary>
    public Guid? UserId { get; set; }

    // === Model versions (snapshot: reconstrucción independiente de la config actual) ===
    public string DetectorVersion { get; set; } = default!;
    public string SegmenterVersion { get; set; } = default!;
    public string ClassifierVersion { get; set; } = default!;
    public string PortionMethod { get; set; } = default!;
    public string? DepthModelVersion { get; set; }

    // === Summary snapshot (nutrición total calculada al momento del análisis) ===
    public decimal? SummaryCalories { get; set; }
    public decimal? SummaryProtein { get; set; }
    public decimal? SummaryCarbohydrates { get; set; }
    public decimal? SummaryFat { get; set; }
    public decimal? SummaryFiber { get; set; }
    public decimal? SummarySugar { get; set; }
    public decimal? SummarySodium { get; set; }
    public string? Source { get; set; }
    public string? SourceVersion { get; set; }

    public ICollection<FoodAnalysisItem> Items { get; set; } = [];
    public ICollection<FoodAnalysisFeedback> Feedbacks { get; set; } = [];
}