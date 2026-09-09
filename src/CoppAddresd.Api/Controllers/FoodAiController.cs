using System.Security.Claims;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.FoodAi;
using CoppAddresd.Application.Features.FoodAi;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.FoodAi;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[AllowAnonymous]
public class FoodAiController : ControllerBase
{
    private static readonly HashSet<string> FeedbackTypes = new(StringComparer.Ordinal)
    {
        "FOOD_WRONG", "PORTION_WRONG", "DETECTION_WRONG", "MISSING_FOOD", "OTHER",
    };

    private readonly IFoodAiClient _foodAiClient;
    private readonly IFoodAnalysisRepository _analysisRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<FoodAiController> _logger;

    public FoodAiController(
        IFoodAiClient foodAiClient,
        IFoodAnalysisRepository analysisRepository,
        IMediator mediator,
        ILogger<FoodAiController> logger)
    {
        _foodAiClient = foodAiClient;
        _analysisRepository = analysisRepository;
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
            // IFormFile entrega un ReferenceReadStream (lectura forward-only, sin
            // seek); el handler re-posiciona el stream (Position = 0) varias veces
            // (guardar imagen + enviar al Food AI). Se bufferiza a MemoryStream.
            await using var sourceStream = image.OpenReadStream();
            using var stream = new MemoryStream();
            await sourceStream.CopyToAsync(stream, ct);
            stream.Position = 0;
            var userId = TryGetUserId();
            var command = new AnalyzeFoodImageCommand(
                stream, image.FileName, image.ContentType, image.Length, userId);
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

    /// <summary>
    /// Recupera un análisis persistido (con items, snapshots y feedbacks).
    /// [Authorize] + ownership: un usuario solo ve sus análisis; los análisis
    /// anónimos (sin user) requieren el mismo dueño (ninguno) → 404.
    /// </summary>
    [HttpGet("analyses/{analysisId:guid}")]
    [Authorize]
    public async Task<ActionResult> GetAnalysis(Guid analysisId, CancellationToken ct)
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var analysis = await _analysisRepository.GetByAnalysisIdAsync(analysisId, ct);
        if (analysis is null || (analysis.UserId is not null && analysis.UserId != userId))
        {
            // 404: no se revela existencia de análisis ajenos.
            return NotFound(ErrorResponse("ANALYSIS_NOT_FOUND", "Análisis no encontrado."));
        }

        return Ok(MapAnalysis(analysis));
    }

    /// <summary>
    /// Guarda la corrección del usuario sobre un análisis. Conserva el valor
    /// ORIGINAL y el CORREGIDO (no sobrescribe). [Authorize] + ownership.
    /// </summary>
    [HttpPost("analyses/{analysisId:guid}/feedback")]
    [Authorize]
    public async Task<ActionResult> PostFeedback(
        Guid analysisId,
        [FromBody] FoodFeedbackRequest request,
        CancellationToken ct)
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Type))
        {
            return BadRequest(ErrorResponse("INVALID_FEEDBACK", "El tipo de feedback es obligatorio."));
        }

        if (!FeedbackTypes.Contains(request.Type))
        {
            return BadRequest(ErrorResponse(
                "INVALID_FEEDBACK_TYPE",
                $"Tipo no soportado: {request.Type}. Válidos: {string.Join(", ", FeedbackTypes)}."));
        }

        var analysis = await _analysisRepository.GetByAnalysisIdAsync(analysisId, ct);
        if (analysis is null || (analysis.UserId is not null && analysis.UserId != userId))
        {
            return NotFound(ErrorResponse("ANALYSIS_NOT_FOUND", "Análisis no encontrado."));
        }

        FoodAnalysisItem? item = null;
        if (request.ItemIndex is not null)
        {
            item = analysis.Items.FirstOrDefault(i => i.ItemIndex == request.ItemIndex);
            if (item is null)
            {
                return BadRequest(ErrorResponse("ITEM_NOT_FOUND", $"Item {request.ItemIndex} no existe en el análisis."));
            }
        }

        var feedback = new FoodAnalysisFeedback
        {
            AnalysisId = analysis.Id,
            ItemId = item?.Id,
            UserId = userId.Value,
            FeedbackType = request.Type,
            OriginalFood = item?.Name,
            CorrectedFood = request.CorrectedFood,
            OriginalGrams = item?.EstimatedGrams,
            CorrectedGrams = request.CorrectedGrams,
            Note = request.Note,
        };

        await _analysisRepository.AddFeedbackAsync(feedback, ct);

        return CreatedAtAction(nameof(GetAnalysis), new { analysisId }, new
        {
            feedbackId = feedback.Id,
            analysisId,
            type = feedback.FeedbackType,
            itemIndex = request.ItemIndex,
            originalFood = feedback.OriginalFood,
            correctedFood = feedback.CorrectedFood,
            originalGrams = feedback.OriginalGrams,
            correctedGrams = feedback.CorrectedGrams,
            createdAt = feedback.CreatedAt,
        });
    }

    private Guid? TryGetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static object MapAnalysis(FoodAnalysis analysis) => new
    {
        analysisId = analysis.AnalysisId,
        status = analysis.Status,
        createdAt = analysis.CreatedAt,
        imageKey = analysis.ImageKey,
        modelVersions = new
        {
            detector = analysis.DetectorVersion,
            segmenter = analysis.SegmenterVersion,
            classifier = analysis.ClassifierVersion,
            portionMethod = analysis.PortionMethod,
            depth = analysis.DepthModelVersion,
        },
        foods = analysis.Items.OrderBy(i => i.ItemIndex).Select(i => new
        {
            name = i.Name,
            confidence = i.DetectionConfidence,
            boundingBox = new { x = i.BboxX, y = i.BboxY, width = i.BboxWidth, height = i.BboxHeight },
            maskKey = i.MaskKey,
            maskAreaPixels = i.MaskAreaPixels,
            portion = i.PortionSize is null ? null : new
            {
                portionSize = i.PortionSize,
                estimatedGrams = i.EstimatedGrams,
                minGrams = i.MinGrams,
                maxGrams = i.MaxGrams,
                confidence = i.PortionConfidence,
                method = i.PortionMethod,
            },
            nutrition = i.NutritionStatus == "available" && i.Calories is not null ? new
            {
                calories = i.Calories,
                protein = i.Protein,
                carbohydrates = i.Carbohydrates,
                fat = i.Fat,
                fiber = i.Fiber,
                sugar = i.Sugar,
                sodium = i.Sodium,
            } : null,
            nutritionStatus = i.NutritionStatus,
            source = i.Source,
            sourceVersion = i.SourceVersion,
        }),
        summary = analysis.SummaryCalories is null ? null : new
        {
            calories = analysis.SummaryCalories,
            protein = analysis.SummaryProtein,
            carbohydrates = analysis.SummaryCarbohydrates,
            fat = analysis.SummaryFat,
            fiber = analysis.SummaryFiber,
            sugar = analysis.SummarySugar,
            sodium = analysis.SummarySodium,
        },
        source = analysis.Source,
        sourceVersion = analysis.SourceVersion,
        feedbacks = analysis.Feedbacks.OrderBy(f => f.CreatedAt).Select(f => new
        {
            id = f.Id,
            type = f.FeedbackType,
            itemIndex = f.ItemId is null ? (int?)null : analysis.Items.FirstOrDefault(i => i.Id == f.ItemId)?.ItemIndex,
            originalFood = f.OriginalFood,
            correctedFood = f.CorrectedFood,
            originalGrams = f.OriginalGrams,
            correctedGrams = f.CorrectedGrams,
            note = f.Note,
            createdAt = f.CreatedAt,
        }),
    };

    private static object ErrorResponse(string code, string message) => new
    {
        error = new { code, message },
    };
}