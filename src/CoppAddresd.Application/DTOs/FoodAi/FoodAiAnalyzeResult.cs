namespace CoppAddresd.Application.DTOs.FoodAi;

/// <summary>Resultado de enviar una imagen al Food AI Service para análisis.</summary>
public record FoodAiAnalyzeResult(string AnalysisId, string Status);