using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.FoodAi;

public class AnalyzeFoodImageCommandHandler
    : IRequestHandler<AnalyzeFoodImageCommand, AnalyzeFoodImageResult>
{
    private readonly ImageFileValidator _validator;
    private readonly IImageStorage _imageStorage;
    private readonly IFoodAiClient _foodAiClient;
    private readonly ILogger<AnalyzeFoodImageCommandHandler> _logger;

    public AnalyzeFoodImageCommandHandler(
        ImageFileValidator validator,
        IImageStorage imageStorage,
        IFoodAiClient foodAiClient,
        ILogger<AnalyzeFoodImageCommandHandler> logger)
    {
        _validator = validator;
        _imageStorage = imageStorage;
        _foodAiClient = foodAiClient;
        _logger = logger;
    }

    public async Task<AnalyzeFoodImageResult> Handle(
        AnalyzeFoodImageCommand request,
        CancellationToken ct)
    {
        var validation = _validator.Validate(
            request.ImageStream, request.FileName, request.ContentType, request.Length);
        if (!validation.IsValid)
        {
            _logger.LogInformation(
                "Imagen rechazada en ingesta: {Code} ({File})",
                validation.ErrorCode, request.FileName);
            throw new InvalidImageException(validation.ErrorCode!, validation.ErrorMessage!);
        }

        var analysisId = Guid.NewGuid();
        _logger.LogInformation(
            "Ingesta de imagen: AnalysisId={AnalysisId}, File={File}, Bytes={Bytes}",
            analysisId, request.FileName, request.Length);

        request.ImageStream.Position = 0;
        await _imageStorage.SaveImageAsync(analysisId, request.FileName, request.ImageStream, ct);

        request.ImageStream.Position = 0;
        var result = await _foodAiClient.SendImageAsync(
            analysisId, request.ImageStream, request.FileName, request.ContentType, ct);

        return new AnalyzeFoodImageResult(
            analysisId.ToString(), result.Status, result.ModelVersion, result.SegModelVersion,
            result.ClassifierVersion, result.InferenceTimeMs, result.Foods);
    }
}