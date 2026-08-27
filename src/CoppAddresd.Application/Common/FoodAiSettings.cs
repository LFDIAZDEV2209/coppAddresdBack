namespace CoppAddresd.Application.Common;

/// <summary>
/// Configuración del Food AI Service (Python/FastAPI). Sección "FoodAi" en
/// appsettings.json o variables de entorno con prefijo FoodAi__.
/// </summary>
public class FoodAiSettings
{
    public const string SectionName = "FoodAi";

    public string BaseUrl { get; set; } = "http://localhost:8010";
    public int TimeoutSeconds { get; set; } = 10;
    public string HealthEndpoint { get; set; } = "/health";
    public string AnalyzeEndpoint { get; set; } = "/analyze";

    /// <summary>Tamaño máximo de imagen aceptado (10 MB por defecto).</summary>
    public long MaxImageSizeBytes { get; set; } = 10 * 1024 * 1024;

    public List<string> AllowedContentTypes { get; set; } = ["image/jpeg", "image/png", "image/webp"];
    public List<string> AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp"];
}