namespace CoppAddresd.Application.DTOs.FoodAi;

/// <summary>Resultado del probe de salud del Food AI Service.</summary>
public record FoodAiHealthStatus(bool IsHealthy, string? Detail = null);