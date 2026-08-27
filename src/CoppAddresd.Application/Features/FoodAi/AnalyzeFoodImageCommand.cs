using CoppAddresd.Application.DTOs.FoodAi;
using MediatR;

namespace CoppAddresd.Application.Features.FoodAi;

/// <summary>
/// Ingesta de una imagen de comida: validación → almacenamiento → detección
/// en el Food AI Service. Respuesta con alimentos detectados (clase +
/// confidence + bounding box en píxeles).
/// </summary>
public record AnalyzeFoodImageCommand(
    Stream ImageStream,
    string FileName,
    string ContentType,
    long Length)
    : IRequest<AnalyzeFoodImageResult>;

public record AnalyzeFoodImageResult(
    string AnalysisId,
    string Status,
    string ModelVersion,
    string SegModelVersion,
    int InferenceTimeMs,
    IReadOnlyList<DetectedFoodDto> Foods);