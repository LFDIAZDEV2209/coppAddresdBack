using CoppAddresd.Application.DTOs.FoodAi;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Cliente del Food AI Service (Python/FastAPI). Desacopla los controllers
/// del HTTP client: cambiar el servicio no implica tocar la capa HTTP.
/// </summary>
public interface IFoodAiClient
{
    Task<FoodAiHealthStatus> GetHealthAsync(CancellationToken ct = default);
}