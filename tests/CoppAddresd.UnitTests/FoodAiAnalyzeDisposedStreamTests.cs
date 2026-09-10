using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.FoodAi;
using CoppAddresd.Application.Features.FoodAi;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.FoodAi;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Regresión del bug de producción (2026-09-10): el stream del IFormFile
/// (ReferenceReadStream de Kestrel) llega DISPOSED al AnalyzeFoodImageCommandHandler,
/// que intenta rebobinarlo (Position = 0) antes de guardarlo y de reenviarlo al
/// Food AI Service → ObjectDisposedException → 500 en POST /api/v1/foodai/analyze.
/// Los tests de endpoint no lo detectaron porque TestServer entrega streams
/// seekable en memoria; aquí se simula el stream real (no seekable, Position
/// lanza tras el dispose del request) y se valida que el controller lo buffera
/// en memoria antes de despachar el comando.
/// </summary>
public class FoodAiAnalyzeDisposedStreamTests
{
    /// <summary>
    /// Stream equivalente al ReferenceReadStream post-dispose: lectura
    /// secuencial permitida (binding multipart ya la consumió, pero un
    /// CopyTo tardío aún funciona con el wrapper del controller); cualquier
    /// cambio de Position lanza ObjectDisposedException, igual que Kestrel.
    /// </summary>
    private sealed class DisposedRequestStream : Stream
    {
        private readonly byte[] _content;
        private int _position;

        public DisposedRequestStream(byte[] content) => _content = content;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _content.Length;
        public override long Position
        {
            get => _position;
            set => throw new ObjectDisposedException(
                "Microsoft.AspNetCore.Http.ReferenceReadStream",
                "Cannot access a disposed object. Object name: 'Microsoft.AspNetCore.Http.ReferenceReadStream'.");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = _content.Length - _position;
            var toRead = Math.Min(count, remaining);
            Array.Copy(_content, _position, buffer, offset, toRead);
            _position += toRead;
            return toRead;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

    private sealed class DisposedFormFile : IFormFile
    {
        private readonly DisposedRequestStream _stream;

        public DisposedFormFile(byte[] bytes, string fileName)
        {
            _stream = new DisposedRequestStream(bytes);
            FileName = fileName;
            Length = bytes.Length;
        }

        public string ContentType { get; } = "image/png";
        public string? ContentDisposition => null;
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length { get; }
        public string Name { get; } = "image";
        public string FileName { get; }

        public Stream OpenReadStream() => _stream;
        public Stream CreateReadStream() => _stream;
        public void CopyTo(Stream destination) =>
            throw new NotSupportedException("El controller usa CopyToAsync.");
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken) =>
            _stream.CopyToAsync(target, cancellationToken);
    }

    private sealed class StubFoodAiClient : IFoodAiClient
    {
        public Task<FoodAiHealthStatus> GetHealthAsync(CancellationToken ct = default) =>
            Task.FromResult(new FoodAiHealthStatus(true, "food-ai-service"));

        public Task<FoodAiAnalyzeResult> SendImageAsync(
            Guid analysisId,
            Stream image,
            string fileName,
            string contentType,
            CancellationToken ct = default)
        {
            using var buffered = new MemoryStream();
            image.CopyTo(buffered);
            return Task.FromResult(new FoodAiAnalyzeResult(
                analysisId.ToString(),
                "completed",
                "food-detector-v1",
                "food-segmenter-v1",
                "detector-based-v1",
                182,
                [new DetectedFoodDto("pizza", 0.94, new BoundingBoxDto(0, 0, 10, 10))]));
        }
    }

    private sealed class StubImageStorage : IImageStorage
    {
        public Task<string> SaveImageAsync(
            Guid analysisId, string fileName, Stream content, CancellationToken ct = default) =>
            Task.FromResult($"foodai/{analysisId:N}.png");

        public Task<string> SaveMaskAsync(
            Guid analysisId, int itemIndex, Stream pngContent, CancellationToken ct = default) =>
            Task.FromResult($"foodai/masks/{analysisId:N}/{itemIndex}.png");
    }

    private sealed class StubNutritionProvider : INutritionProvider
    {
        public Task<FoodNutritionDto?> GetNutritionAsync(
            string foodKey, CancellationToken ct = default) =>
            Task.FromResult<FoodNutritionDto?>(null);
    }

    private sealed class StubNutritionCalculator : INutritionCalculator
    {
        public FoodNutritionResult Calculate(
            FoodNutritionDto per100g, int? estimatedGrams, int? minGrams, int? maxGrams) =>
            throw new NotSupportedException("Sin porción no se invoca.");
    }

    private sealed class StubAnalysisRepository : IFoodAnalysisRepository
    {
        public Task<FoodAnalysis?> GetByAnalysisIdAsync(Guid analysisId, CancellationToken ct = default) =>
            Task.FromResult<FoodAnalysis?>(null);

        public Task<FoodAnalysis> AddAsync(FoodAnalysis analysis, CancellationToken ct = default) =>
            Task.FromResult(analysis);

        public Task AddFeedbackAsync(FoodAnalysisFeedback feedback, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// MediatR mínimo: el controller despacha AnalyzeFoodImageCommand y el
    /// comando lo ejecuta el handler REAL (misma ruta que producción sin
    /// pipeline de behaviors: la validación corre dentro del handler).
    /// </summary>
    private sealed class FakeMediator : IMediator
    {
        private readonly AnalyzeFoodImageCommandHandler _handler;

        public FakeMediator(AnalyzeFoodImageCommandHandler handler) => _handler = handler;

        public async Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request, CancellationToken cancellationToken = default)
            where TResponse : notnull
        {
            if (request is AnalyzeFoodImageCommand command)
            {
                var result = await _handler.Handle(command, cancellationToken);
                return (TResponse)(object)result;
            }

            throw new NotSupportedException($"Request no soportado: {request.GetType().Name}");
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            where TResponse : notnull => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        Task ISender.Send<TRequest>(TRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task Publish<TNotification>(
            TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => throw new NotSupportedException();

        public Task Publish(
            object notification, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static FoodAiController CreateController()
    {
        var settings = Options.Create(new FoodAiSettings());
        var handler = new AnalyzeFoodImageCommandHandler(
            new ImageFileValidator(settings),
            new StubImageStorage(),
            new StubFoodAiClient(),
            new StubNutritionProvider(),
            new StubNutritionCalculator(),
            new StubAnalysisRepository(),
            NullLogger<AnalyzeFoodImageCommandHandler>.Instance);
        return new FoodAiController(
            new StubFoodAiClient(),
            new StubAnalysisRepository(),
            new FakeMediator(handler),
            NullLogger<FoodAiController>.Instance)
        {
            // TryGetUserId() lee el HttpContext: se provee uno vacío.
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext(),
            },
        };
    }

    private static readonly byte[] Png1x1 = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [Fact]
    public async Task Analyze_stream_request_disposed_se_bufferea_y_responde_200()
    {
        var controller = CreateController();
        var formFile = new DisposedFormFile(Png1x1, "bandeja.png");

        var response = await controller.Analyze(formFile, CancellationToken.None);

        var ok = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(response.Result);
        var body = ok.Value!;
        var type = body.GetType();
        Assert.True(
            Guid.TryParse((string?)type.GetProperty("analysisId")!.GetValue(body), out _));
        Assert.Equal("completed", (string?)type.GetProperty("status")!.GetValue(body));
        var foods =
            Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<object>>(
                type.GetProperty("foods")!.GetValue(body));
        var first = Assert.IsAssignableFrom<object>(foods.First());
        Assert.Equal("pizza", first.GetType().GetProperty("name")!.GetValue(first));
    }

    [Fact]
    public async Task Analyze_stream_disposed_nunca_llega_al_handler_sin_buffer()
    {
        // El stream no-seekable NO debe entregarse tal cual al handler: el
        // controller lo buffera (MemoryStream seekable) antes de armar el
        // comando. Sin el fix, el primer Position = 0 del handler lanza
        // ObjectDisposedException y este test falla (regresión del bug).
        var controller = CreateController();
        var formFile = new DisposedFormFile(Png1x1, "x.png");

        var response = await controller.Analyze(formFile, CancellationToken.None);

        Assert.NotNull(response.Result);
        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(response.Result);
    }
}
