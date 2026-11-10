using System.Text;
using System.Text.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.LabExam;
using CoppAddresd.Application.Features.LabExam;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CoppAddresd.UnitTests.LabExam;

public sealed class UploadLabExamCommandHandlerTests
{
    private static readonly DateTime PrintedDate = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
    private readonly IFileCompressionService compression = Substitute.For<IFileCompressionService>();
    private readonly IObjectStorageService storage = Substitute.For<IObjectStorageService>();
    private readonly IAiServiceClient ai = Substitute.For<IAiServiceClient>();
    private readonly IClinicalMeasurementRepository measurements = Substitute.For<IClinicalMeasurementRepository>();
    private readonly IPatientRepository patients = Substitute.For<IPatientRepository>();
    private readonly IDocumentRepository documents = Substitute.For<IDocumentRepository>();
    private readonly IProgramControlRepository controls = Substitute.For<IProgramControlRepository>();
    private readonly PatientProfile patient = new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ClinicId = Guid.NewGuid() };
    private readonly ClinicalDocumentType documentType = new() { Id = Guid.NewGuid(), Code = "OTHER", IsActive = true, AllowedExtensions = ["pdf"] };
    private readonly MeasurementMetric metric = new() { Id = Guid.NewGuid(), Code = "systolic_bp", Name = "Presión sistólica" };
    private readonly Dictionary<string, byte[]> objects = [];
    private readonly byte[] originalBytes = Encoding.UTF8.GetBytes("ORIGINAL DOCUMENT, NOT COMPRESSED");
    private readonly byte[] compressedBytes = Encoding.UTF8.GetBytes("OCR IMAGE");
    private Document? savedDocument;

    public UploadLabExamCommandHandlerTests()
    {
        patients.GetByUserIdAsync(patient.UserId!.Value, Arg.Any<CancellationToken>()).Returns(patient);
        documents.ListTypesAsync(null, true, Arg.Any<CancellationToken>()).Returns(new[] { documentType });
        documents.AddAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>())
            .Returns(call => { savedDocument = call.Arg<Document>(); return Task.CompletedTask; });
        compression.CompressAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => ((Stream)new MemoryStream(compressedBytes), "image/jpeg"));
        storage.PutDocumentArchiveObjectAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                using var copy = new MemoryStream();
                await call.Arg<Stream>().CopyToAsync(copy);
                objects[call.ArgAt<string>(0)] = copy.ToArray();
                return call.ArgAt<string>(0);
            });
        measurements.GetActiveMetricsWithUnitsAsync(Arg.Any<CancellationToken>()).Returns(new[] { metric });
        measurements.GetLastPerMetricAsync(patient.Id, Arg.Any<IEnumerable<string>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, LabExamMetricSnapshot>());
        SetExtraction(new LabExamAiMetricDto("presión sistólica", 60m, "mmHg", PrintedDate, "60 mmHg", 1));
    }

    private UploadLabExamCommandHandler Handler()
    {
        // Registra también el módulo de controles habilitado: la extracción pendiente
        // no debe interactuar con él, aunque exista un control abierto.
        var services = new ServiceCollection()
            .AddSingleton(compression).AddSingleton(storage).AddSingleton(ai)
            .AddSingleton(measurements).AddSingleton(patients).AddSingleton(documents)
            .AddSingleton(controls)
            .AddSingleton<IOptions<ProgramControlSettings>>(Options.Create(new ProgramControlSettings { ControlsEnabled = true }))
            .AddSingleton<ILogger<UploadLabExamCommandHandler>>(NullLogger<UploadLabExamCommandHandler>.Instance)
            .BuildServiceProvider();
        return ActivatorUtilities.CreateInstance<UploadLabExamCommandHandler>(services);
    }

    private UploadLabExamCommand Request(string language = "es") => new(
        new MemoryStream(originalBytes), "original.pdf", "application/pdf", originalBytes.Length,
        patient.UserId!.Value, "thread-fixture", language);

    private void SetExtraction(params LabExamAiMetricDto[] metrics)
        => ai.ExtractLabMetricsAsync(patient.Id, Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(),
            Arg.Any<string>(), "thread-fixture", Arg.Any<CancellationToken>())
            .Returns(new LabExamAiResponse("Tu presión mejoró; ya guardamos tus mediciones.", metrics));

    [Fact]
    public async Task Handle_Candidatos_ArchivaOriginalYCandidatosSinEstablecerMediciones()
    {
        using var cts = new CancellationTokenSource();
        byte[]? sentToOcr = null;
        ai.When(client => client.ExtractLabMetricsAsync(patient.Id, Arg.Any<Guid>(), Arg.Any<Stream>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), cts.Token))
            .Do(call => { using var copy = new MemoryStream(); call.Arg<Stream>().CopyTo(copy); sentToOcr = copy.ToArray(); });
        measurements.GetLastPerMetricAsync(patient.Id, Arg.Any<IEnumerable<string>>(), Arg.Any<Guid>(), cts.Token)
            .Returns(new Dictionary<string, LabExamMetricSnapshot>
            { [metric.Code] = new(metric.Code, 120m, "mmHg", PrintedDate.AddDays(-1), "professional") });

        var result = await Handler().Handle(Request(), cts.Token);

        Assert.Equal("pending_review", result.VerificationStatus);
        Assert.Equal(0, result.MeasurementCount);
        Assert.Equal($"documents/{patient.Id}/{result.DocumentId}/original.pdf", result.StorageKey);
        Assert.Equal($"documents/{patient.Id}/{result.DocumentId}/extraction.json", result.ExtractionStorageKey);
        Assert.Equal(originalBytes, objects[result.StorageKey!]);
        Assert.Equal(compressedBytes, sentToOcr);
        Assert.NotNull(savedDocument);
        Assert.Equal(result.DocumentId, savedDocument.Id);
        Assert.Equal(result.BatchId, savedDocument.Id);
        Assert.Equal(patient.Id, savedDocument.PatientId);
        Assert.Equal(patient.ClinicId, savedDocument.ClinicId);
        Assert.Equal(documentType.Id, savedDocument.DocumentTypeId);
        Assert.Equal(patient.UserId, savedDocument.UploadedBy);
        Assert.Equal("original.pdf", savedDocument.Title);
        Assert.Equal("Ready", savedDocument.Status); // disponibilidad del archivo, no aprobación.
        Assert.Equal(originalBytes.Length, savedDocument.FileSizeBytes);
        Assert.Equal("application/pdf", savedDocument.ContentType);
        Assert.DoesNotContain("documents/", savedDocument.Description);
        Assert.DoesNotContain(".json", savedDocument.Description);
        Assert.Contains("requieren verificación", savedDocument.Description);
        var candidate = Assert.Single(result.ExtractedMetrics!);
        Assert.Equal("ocr_unverified", candidate.Source);
        Assert.Equal("60 mmHg", candidate.SourceText);
        Assert.Equal(1, candidate.SourcePage);
        Assert.Equal(PrintedDate, candidate.ObservedAt);
        var comparison = Assert.Single(result.Comparisons!).Value;
        Assert.Equal("decrease", comparison.Change);
        Assert.Equal(-60m, comparison.Delta);
        Assert.Equal("professional", comparison.PreviousSource);
        Assert.Equal("ocr_unverified", comparison.CurrentSource);
        using var metadata = JsonDocument.Parse(objects[result.ExtractionStorageKey!]);
        Assert.Equal(result.DocumentId.ToString(), metadata.RootElement.GetProperty("documentId").GetString());
        Assert.Equal("pending_review", metadata.RootElement.GetProperty("verificationStatus").GetString());
        Assert.Equal("ocr_unverified", metadata.RootElement.GetProperty("extractedMetrics")[0].GetProperty("source").GetString());
        Assert.Equal("decrease", metadata.RootElement.GetProperty("comparisons").GetProperty("systolic_bp").GetProperty("change").GetString());
        Assert.DoesNotContain("mejoró", result.Summary);
        await measurements.DidNotReceiveWithAnyArgs().AddBatchAsync(default!, default);
        await measurements.DidNotReceiveWithAnyArgs().GetActiveUnitsAsync(default);
        await ai.DidNotReceiveWithAnyArgs().NarrateLabExamAsync(default, default, default!, default!, default, default);
        await ai.DidNotReceiveWithAnyArgs().ProactiveMessageAsync(default, default!, default!, default!, default);
        await documents.Received(1).AddAsync(Arg.Any<Document>(), cts.Token);
    }

    [Fact]
    public async Task Handle_ControlAbierto_NoCompletaControlNiLoVinculaAlCandidato()
    {
        var control = new ProgramControl { Id = Guid.NewGuid(), Status = ProgramControlStatus.Responded };
        controls.FindOpenControlForUserAsync(patient.UserId!.Value, "thread-fixture", Arg.Any<CancellationToken>()).Returns(control);

        var result = await Handler().Handle(Request(), CancellationToken.None);

        Assert.Equal("pending_review", result.VerificationStatus);
        Assert.Equal(ProgramControlStatus.Responded, control.Status);
        Assert.Empty(controls.ReceivedCalls());
        await measurements.DidNotReceiveWithAnyArgs().AddBatchAsync(default!, default);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task Handle_SinUnidadOFecha_NoInfiereDatosNiCreaMediciones(bool hasUnit, bool hasDate)
    {
        SetExtraction(new LabExamAiMetricDto("presión sistólica", 60m, hasUnit ? "mmHg" : null, hasDate ? PrintedDate : null));
        measurements.GetLastPerMetricAsync(patient.Id, Arg.Any<IEnumerable<string>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, LabExamMetricSnapshot>
            { [metric.Code] = new(metric.Code, 120, "mmHg", PrintedDate.AddDays(-1), "device") });

        var result = await Handler().Handle(Request(), CancellationToken.None);

        var candidate = Assert.Single(result.ExtractedMetrics!);
        Assert.Equal(hasUnit ? "mmHg" : null, candidate.UnitSymbol);
        Assert.Equal(hasDate ? PrintedDate : null, candidate.ObservedAt);
        Assert.Equal(0, result.MeasurementCount);
        Assert.Equal("not_comparable", Assert.Single(result.Comparisons!).Value.Change);
        Assert.Null(Assert.Single(result.Comparisons!).Value.Delta);
        await measurements.DidNotReceiveWithAnyArgs().AddBatchAsync(default!, default);
        await measurements.DidNotReceiveWithAnyArgs().GetActiveUnitsAsync(default);
        Assert.Empty(controls.ReceivedCalls());
    }

    [Fact]
    public async Task Handle_MetricaDesconocida_ConservaCandidatoSinAsignarCatalogo()
    {
        SetExtraction(new LabExamAiMetricDto("texto sin correspondencia", 10m, "unidad impresa", null));

        var result = await Handler().Handle(Request(), CancellationToken.None);

        Assert.Null(Assert.Single(result.ExtractedMetrics!).MetricCode);
        Assert.Empty(result.DetectedMetrics);
        Assert.Empty(result.Comparisons!);
        Assert.Equal(0, result.MeasurementCount);
        Assert.NotEmpty(objects[result.ExtractionStorageKey!]);
        await measurements.DidNotReceiveWithAnyArgs().GetLastPerMetricAsync(default, default!, default, default);
    }

    [Theory]
    [InlineData(false, "unreadable")]
    [InlineData(true, "no_metrics")]
    public async Task Handle_SinValoresLegibles_ConservaOriginalYNoConfirmaProcesamiento(bool readable, string status)
    {
        ai.ExtractLabMetricsAsync(patient.Id, Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(),
            Arg.Any<string>(), "thread-fixture", Arg.Any<CancellationToken>())
            .Returns(new LabExamAiResponse("Everything improved", [], readable));

        var result = await Handler().Handle(Request("en"), CancellationToken.None);

        Assert.Equal(status, result.VerificationStatus);
        Assert.Equal(0, result.MeasurementCount);
        Assert.Equal(originalBytes, objects[result.StorageKey!]);
        Assert.NotNull(savedDocument);
        Assert.Contains("No measurements were added", result.Summary);
        Assert.DoesNotContain("improved", result.Summary);
        Assert.Empty(result.ExtractedMetrics!);
        Assert.Empty(controls.ReceivedCalls());
    }

    [Fact]
    public async Task Handle_HistoriaNoDisponible_NoInventaPrimerRegistro()
    {
        measurements.GetLastPerMetricAsync(patient.Id, Arg.Any<IEnumerable<string>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("synthetic fixture"));

        var result = await Handler().Handle(Request(), CancellationToken.None);

        Assert.Equal("pending_review", result.VerificationStatus);
        Assert.Empty(result.Comparisons!);
        Assert.NotNull(savedDocument);
    }

    [Fact]
    public async Task Handle_CatalogoDocumentalAusente_FallaAntesDeArchivoOProveedor()
    {
        documents.ListTypesAsync(null, true, Arg.Any<CancellationToken>()).Returns(Array.Empty<ClinicalDocumentType>());

        await Assert.ThrowsAsync<UnprocessableEntityException>(() => Handler().Handle(Request(), CancellationToken.None));

        Assert.Empty(objects);
        Assert.Empty(ai.ReceivedCalls());
        Assert.Empty(compression.ReceivedCalls());
        Assert.Null(savedDocument);
    }

    [Fact]
    public async Task Handle_FalloPersistenciaDocumental_CompensaObjetosYNoEstableceMediciones()
    {
        documents.AddAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("synthetic fixture"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(Request(), CancellationToken.None));

        Assert.Equal(2, objects.Count);
        foreach (var key in objects.Keys)
            await storage.Received(1).DeleteDocumentArchiveObjectAsync(key, CancellationToken.None);
        await measurements.DidNotReceiveWithAnyArgs().AddBatchAsync(default!, default);
        Assert.Empty(controls.ReceivedCalls());
    }

    [Fact]
    public async Task Handle_CanceladoAntesDeComenzar_NoRealizaEfectos()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Handler().Handle(Request(), cts.Token));

        Assert.Empty(objects);
        Assert.Empty(ai.ReceivedCalls());
        Assert.Empty(documents.ReceivedCalls());
    }

    [Fact]
    public async Task ContratoUpload_ConservaCamposPreviosYAnadeEstadoExplicito()
    {
        var result = await Handler().Handle(Request(), CancellationToken.None);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        foreach (var field in new[] { "batchId", "summary", "measurementCount", "detectedMetrics", "storageKey",
            "verificationStatus", "documentId", "extractionStorageKey", "extractedMetrics", "comparisons" })
            Assert.True(json.RootElement.TryGetProperty(field, out _), field);
        Assert.Equal(0, json.RootElement.GetProperty("measurementCount").GetInt32());
    }

    [Fact]
    public async Task Handle_UsuarioCoincideConIdDeOtroPerfil_ConservaPropiedadDelUsuarioAutenticado()
    {
        patients.GetByIdAsync(patient.UserId!.Value, Arg.Any<CancellationToken>())
            .Returns(new PatientProfile { Id = patient.UserId.Value, UserId = Guid.NewGuid(), ClinicId = Guid.NewGuid() });

        var result = await Handler().Handle(Request(), CancellationToken.None);

        Assert.Equal(patient.Id, savedDocument!.PatientId);
        Assert.StartsWith($"documents/{patient.Id}/", result.StorageKey);
    }

    [Fact]
    public async Task Handle_UsuarioSinPerfil_NoUtilizaUnPerfilAjenoConElMismoId()
    {
        patients.GetByUserIdAsync(patient.UserId!.Value, Arg.Any<CancellationToken>()).Returns((PatientProfile?)null);
        patients.GetByIdAsync(patient.UserId.Value, Arg.Any<CancellationToken>())
            .Returns(new PatientProfile { Id = patient.UserId.Value, UserId = Guid.NewGuid(), ClinicId = Guid.NewGuid() });

        await Assert.ThrowsAsync<CoppAddresd.Domain.Exceptions.NotFoundException>(() => Handler().Handle(Request(), CancellationToken.None));

        Assert.Empty(objects);
        Assert.Empty(ai.ReceivedCalls());
        Assert.Empty(documents.ReceivedCalls());
    }
}
