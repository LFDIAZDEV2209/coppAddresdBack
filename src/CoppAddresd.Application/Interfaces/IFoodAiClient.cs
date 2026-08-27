using CoppAddresd.Application.DTOs.FoodAi;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Cliente del Food AI Service (Python/FastAPI). Desacopla los controllers
/// del HTTP client: cambiar el servicio no implica tocar la capa HTTP.
/// </summary>
public interface IFoodAiClient
{
    Task<FoodAiHealthStatus> GetHealthAsync(CancellationToken ct = default);

    /// <summary>
    /// Envía una imagen para análisis (ingesta). El servicio todavía no
    /// analiza comida: responde status "received". Errores del servicio se
    /// propagan como <see cref="Common.FoodAiException"/>.
    /// </summary>
    Task<FoodAiAnalyzeResult> SendImageAsync(
        Guid analysisId,
        Stream image,
        string fileName,
        string contentType,
        CancellationToken ct = default);
}