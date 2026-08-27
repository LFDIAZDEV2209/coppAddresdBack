using MediatR;

namespace CoppAddresd.Application.Features.FoodAi;

/// <summary>
/// Ingesta de una imagen de comida: validación → almacenamiento → envío al
/// Food AI Service. Respuesta síncrona con status "received" (sin análisis
/// todavía).
/// </summary>
public record AnalyzeFoodImageCommand(
    Stream ImageStream,
    string FileName,
    string ContentType,
    long Length)
    : IRequest<AnalyzeFoodImageResult>;

public record AnalyzeFoodImageResult(string AnalysisId, string Status);