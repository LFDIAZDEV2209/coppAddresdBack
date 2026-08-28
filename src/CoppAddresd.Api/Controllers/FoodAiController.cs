using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.FoodAi;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[AllowAnonymous]
public class FoodAiController : ControllerBase
{
    private readonly IFoodAiClient _foodAiClient;
    private readonly IMediator _mediator;
    private readonly ILogger<FoodAiController> _logger;

    public FoodAiController(
        IFoodAiClient foodAiClient,
        IMediator mediator,
        ILogger<FoodAiController> logger)
    {
        _foodAiClient = foodAiClient;
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Prueba de comunicación backend → Food AI Service. Health público:
    /// no expone datos sensibles.
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult> Health(CancellationToken ct)
    {
        var status = await _foodAiClient.GetHealthAsync(ct);
        return Ok(new
        {
            backend = "healthy",
            foodAI = status.IsHealthy ? "healthy" : "unhealthy",
            detail = status.Detail,
        });
    }

    /// <summary>
    /// Ingesta de imagen de comida (multipart). Valida, almacena y envía al
    /// Food AI Service. Respuesta síncrona: { analysisId, status }.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<AnalyzeFoodImageResult>> Analyze(
        [FromForm] IFormFile image,
        CancellationToken ct)
    {
        if (image is null || image.Length == 0)
        {
            return BadRequest(ErrorResponse(
                ImageFileValidator.CodeEmptyFile, "No se recibió una imagen."));
        }

        try
        {
            await using var stream = image.OpenReadStream();
            var command = new AnalyzeFoodImageCommand(
                stream, image.FileName, image.ContentType, image.Length);
            var result = await _mediator.Send(command, ct);
            return Ok(new
            {
                analysisId = result.AnalysisId,
                status = result.Status,
                modelVersion = result.ModelVersion,
                segModelVersion = result.SegModelVersion,
                classifierVersion = result.ClassifierVersion,
                inferenceTimeMs = result.InferenceTimeMs,
                foods = result.Foods.Select(f => new
                {
                    name = f.Name,
                    confidence = f.Confidence,
                    boundingBox = f.BoundingBox,
                    segmentation = f.Segmentation,
                    portion = f.Portion,
                    nutrition = f.NutritionResult?.Nutrition,
                    nutritionRange = f.NutritionResult?.NutritionRange,
                    nutritionStatus = f.NutritionResult?.NutritionStatus,
                    source = f.NutritionResult?.Source,
                    sourceVersion = f.NutritionResult?.SourceVersion,
                }),
                summary = result.Summary,
                summaryRange = result.SummaryRange,
            });
        }
        catch (InvalidImageException ex)
        {
            _logger.LogInformation(
                "Imagen inválida en /foodai/analyze: {Code} — {Message}",
                ex.Code, ex.Message);
            return BadRequest(ErrorResponse(ex.Code, ex.Message));
        }
        catch (FoodAiException ex)
        {
            _logger.LogError(ex,
                "Food AI Service rechazó el análisis (status {Status}): {Detail}",
                ex.StatusCode, ex.Detail);
            return StatusCode(502, ErrorResponse(
                "AI_SERVICE_UNAVAILABLE", "El Food AI Service no pudo procesar la imagen."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al analizar imagen de comida");
            return StatusCode(500, ErrorResponse(
                "INTERNAL_ERROR", "No fue posible procesar la solicitud."));
        }
    }

    /// <summary>
    /// Nutrición por 100 g de un alimento (alias del modelo o nombre canónico).
    /// Consulta la BD nutricional (USDA FDC); no usa IA. 404 controlado si el
    /// alimento no existe o no tiene entrada.
    /// </summary>
    [HttpGet("nutrition/{foodKey}")]
    public async Task<ActionResult<CoppAddresd.Application.DTOs.FoodAi.FoodNutritionDto>> Nutrition(
        string foodKey,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetFoodNutritionQuery(foodKey), ct);
        if (result is null)
        {
            return NotFound(ErrorResponse(
                "FOOD_NOT_FOUND", $"No hay información nutricional para '{foodKey}'."));
        }

        return Ok(result);
    }

    private static object ErrorResponse(string code, string message) => new
    {
        error = new { code, message },
    };
}