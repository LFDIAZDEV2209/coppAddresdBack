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
}