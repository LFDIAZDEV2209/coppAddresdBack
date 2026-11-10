using System.Globalization;
using System.Text;
using System.Text.Json;
using CoppAddresd.Application.DTOs.LabExam;
using CoppAddresd.Application.Exceptions;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.LabExam;

/// <summary>
/// Orquestador de la subida y procesamiento de exámenes de laboratorio:
/// Conserva el archivo original en el repositorio documental y la extracción OCR
/// como candidatos pendientes. Una extracción automática nunca establece mediciones
/// clínicas, completa controles ni entra como hecho confirmado al contexto del chat.
/// </summary>
public class UploadLabExamCommandHandler
    : IRequestHandler<UploadLabExamCommand, LabExamUploadResult>
{
    private static readonly Dictionary<string, string> MetricAliases = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["glucose_fasting"] = "glucose_fasting",
        ["glucosa en ayunas"] = "glucose_fasting",
        ["glucosa"] = "glucose_fasting",
        ["glicemia"] = "glucose_fasting",
        ["glicemia en ayunas"] = "glucose_fasting",
        ["fasting glucose"] = "glucose_fasting",
        ["blood glucose"] = "glucose_fasting",

        ["hba1c"] = "hba1c",
        ["hemoglobina glicosilada"] = "hba1c",
        ["hemoglobina glicada"] = "hba1c",
        ["a1c"] = "hba1c",
        ["glycated hemoglobin"] = "hba1c",

        ["systolic_bp"] = "systolic_bp",
        ["presion arterial sistolica"] = "systolic_bp",
        ["presión arterial sistólica"] = "systolic_bp",
        ["presion sistolica"] = "systolic_bp",
        ["presión sistólica"] = "systolic_bp",
        ["pas"] = "systolic_bp",
        ["systolic"] = "systolic_bp",
        ["systolic blood pressure"] = "systolic_bp",

        ["diastolic_bp"] = "diastolic_bp",
        ["presion arterial diastolica"] = "diastolic_bp",
        ["presión arterial diastólica"] = "diastolic_bp",
        ["presion diastolica"] = "diastolic_bp",
        ["presión diastólica"] = "diastolic_bp",
        ["pad"] = "diastolic_bp",
        ["diastolic"] = "diastolic_bp",
        ["diastolic blood pressure"] = "diastolic_bp",

        ["heart_rate"] = "heart_rate",
        ["frecuencia cardiaca"] = "heart_rate",
        ["frecuencia cardíaca"] = "heart_rate",
        ["pulso"] = "heart_rate",
        ["heart rate"] = "heart_rate",
        ["pulse"] = "heart_rate",

        ["o2_saturation"] = "o2_saturation",
        ["saturacion de oxigeno"] = "o2_saturation",
        ["saturación de oxígeno"] = "o2_saturation",
        ["saturacion oxigeno"] = "o2_saturation",
        ["saturación oxígeno"] = "o2_saturation",
        ["spo2"] = "o2_saturation",
        ["oxygen saturation"] = "o2_saturation",

        ["temperature_c"] = "temperature_c",
        ["temperatura"] = "temperature_c",
        ["temperatura corporal"] = "temperature_c",
        ["temp"] = "temperature_c",
        ["temperature"] = "temperature_c",
        ["body temperature"] = "temperature_c",

        ["weight"] = "weight",
        ["peso"] = "weight",
        ["peso corporal"] = "weight",
        ["body weight"] = "weight",

        ["height"] = "height",
        ["talla"] = "height",
        ["estatura"] = "height",
        ["altura"] = "height",

        ["bmi"] = "bmi",
        ["imc"] = "bmi",
        ["indice de masa corporal"] = "bmi",
        ["índice de masa corporal"] = "bmi",
        ["body mass index"] = "bmi",

        ["body_fat"] = "body_fat",
        ["grasa corporal"] = "body_fat",
        ["porcentaje grasa corporal"] = "body_fat",
        ["porcentaje de grasa corporal"] = "body_fat",
        ["body fat"] = "body_fat",

        ["waist"] = "waist",
        ["cintura"] = "waist",
        ["perimetro de cintura"] = "waist",
        ["perímetro de cintura"] = "waist",
        ["circunferencia de cintura"] = "waist",
        ["waist circumference"] = "waist",

        ["hip"] = "hip",
        ["cadera"] = "hip",
        ["perimetro de cadera"] = "hip",
        ["perímetro de cadera"] = "hip",
        ["circunferencia de cadera"] = "hip",
        ["hip circumference"] = "hip",

        ["wrist"] = "wrist",
        ["muñeca"] = "wrist",
        ["perimetro de muñeca"] = "wrist",
        ["perímetro de muñeca"] = "wrist",
        ["circunferencia de muñeca"] = "wrist",
        ["wrist circumference"] = "wrist",
    };

    private readonly IFileCompressionService _compressionService;
    private readonly IObjectStorageService _storageService;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly IClinicalMeasurementRepository _measurementRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<UploadLabExamCommandHandler> _logger;

    public UploadLabExamCommandHandler(
        IFileCompressionService compressionService,
        IObjectStorageService storageService,
        IAiServiceClient aiServiceClient,
        IClinicalMeasurementRepository measurementRepository,
        IPatientRepository patientRepository,
        IDocumentRepository documentRepository,
        ILogger<UploadLabExamCommandHandler> logger
    )
    {
        _compressionService = compressionService;
        _storageService = storageService;
        _aiServiceClient = aiServiceClient;
        _measurementRepository = measurementRepository;
        _patientRepository = patientRepository;
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task<LabExamUploadResult> Handle(UploadLabExamCommand request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ValidateInput(request);
        var patient = await _patientRepository.GetByUserIdAsync(request.UserId, ct)
            ?? throw new NotFoundException("No existe un perfil de paciente para este archivo.");

        // El catálogo existente ofrece OTHER; no se inventa un subtipo de examen.
        var originalExtension = Path.GetExtension(request.FileName).ToLowerInvariant();
        var documentType = (await _documentRepository.ListTypesAsync(ct: ct))
            .FirstOrDefault(type => type.Code == "OTHER" && type.IsActive
                && (type.AllowedExtensions.Count == 0 || type.AllowedExtensions.Contains(originalExtension.TrimStart('.'))))
            ?? throw new UnprocessableEntityException("No está disponible el tipo documental necesario para conservar el examen. Contacta a tu equipo de atención.");

        // La compresión solo prepara la entrada OCR. El documento conserva los bytes originales.
        await using var original = new MemoryStream();
        await request.FileStream.CopyToAsync(original, ct);
        if (original.Length == 0) throw new UnprocessableEntityException("El archivo está vacío.");
        if (original.Length > UploadLabExamCommandValidator.MaxRawSizeBytes) throw new LabExamRawFileTooLargeException();
        original.Position = 0;
        var (compressed, compressedContentType) = await _compressionService.CompressAsync(original, request.ContentType, ct);
        await using var compressedLifetime = compressed;
        var batchId = Guid.NewGuid();
        var storageKey = $"documents/{patient.Id}/{batchId}/original{originalExtension}";
        var extractionKey = $"documents/{patient.Id}/{batchId}/extraction.json";
        var attemptedKeys = new List<string>();
        var documentSaved = false;
        try
        {
            original.Position = 0;
            attemptedKeys.Add(storageKey);
            await _storageService.PutDocumentArchiveObjectAsync(storageKey, original, request.ContentType.Split(';')[0].Trim(), ct);
            if (compressed.CanSeek) compressed.Position = 0;
            var response = await _aiServiceClient.ExtractLabMetricsAsync(patient.Id, batchId, compressed,
                Path.GetFileNameWithoutExtension(request.FileName) + ResolveExtension(request.FileName, compressedContentType),
                compressedContentType, request.ThreadId, ct);
            ct.ThrowIfCancellationRequested();

            var catalog = await _measurementRepository.GetActiveMetricsWithUnitsAsync(ct);
            var candidates = response.Metrics.Select(metric => new LabExamExtractedMetricDto(
                metric.MetricName, ResolveMetric(metric.MetricName, catalog)?.Code, metric.Value,
                string.IsNullOrWhiteSpace(metric.UnitSymbol) ? null : metric.UnitSymbol.Trim(), metric.ObservedAt,
                SourceText: metric.SourceText, SourcePage: metric.SourcePage)).ToArray();
            var verificationStatus = !response.Readable ? "unreadable" : candidates.Length == 0 ? "no_metrics" : "pending_review";
            var snapshots = candidates.Where(candidate => candidate.MetricCode is not null)
                .Select(candidate => new LabExamMetricSnapshot(candidate.MetricCode!, candidate.Value, candidate.UnitSymbol,
                    candidate.ObservedAt, candidate.Source)).ToArray();
            var comparisons = await BuildComparisonsAsync(patient.Id, batchId, snapshots, ct);
            var summary = PendingSummary(candidates.Length, verificationStatus, request.Language);

            // Sidecar auditable por referencia documental; no es una cola de revisión ni un resultado clínico.
            var metadata = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1, batchId, documentId = batchId, originalFileName = Path.GetFileName(request.FileName),
                originalStorageKey = storageKey, uploadedAt = DateTime.UtcNow, verificationStatus,
                extractedMetrics = candidates, comparisons,
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            await using var extraction = new MemoryStream(metadata, writable: false);
            attemptedKeys.Add(extractionKey);
            await _storageService.PutDocumentArchiveObjectAsync(extractionKey, extraction, "application/json", ct);
            var title = Path.GetFileName(request.FileName);
            var document = new Document
            {
                Id = batchId, PatientId = patient.Id, ClinicId = patient.ClinicId, DocumentTypeId = documentType.Id,
                Title = title.Length <= 200 ? title : title[..200],
                Description = "Archivo original recibido. Los posibles valores extraídos automáticamente requieren verificación contra el documento. No se añadieron mediciones clínicas ni se completó un control del programa.",
                StorageKey = storageKey, ContentType = request.ContentType.Split(';')[0].Trim(), FileSizeBytes = original.Length,
                Status = "Ready", UploadedBy = request.UserId, CreatedBy = request.UserId, CreatedAt = DateTime.UtcNow,
            };
            await _documentRepository.AddAsync(document, ct);
            documentSaved = true;
            return new LabExamUploadResult(batchId, summary, 0,
                candidates.Where(candidate => candidate.MetricCode is not null)
                    .Select(candidate => catalog.First(metric => metric.Code == candidate.MetricCode).Name).Distinct().ToArray(),
                storageKey, verificationStatus, document.Id, extractionKey, candidates, comparisons);
        }
        catch (Exception error)
        {
            // Antes del único SaveChanges documental se compensan los objetos; nunca se borra un documento confirmado.
            if (!documentSaved)
            {
                foreach (var key in attemptedKeys)
                {
                    try { await _storageService.DeleteDocumentArchiveObjectAsync(key, CancellationToken.None); }
                    catch (Exception cleanupError)
                    {
                        _logger.LogError(cleanupError, "No se pudo compensar el objeto de examen {StorageKey}", key);
                    }
                }
            }
            _logger.LogWarning(error, "No se pudo archivar la extracción pendiente del examen {BatchId}", batchId);
            throw;
        }
    }

    private async Task<IReadOnlyDictionary<string, MetricEvolution>> BuildComparisonsAsync(
        Guid patientId, Guid batchId, IReadOnlyList<LabExamMetricSnapshot> snapshots, CancellationToken ct)
    {
        if (snapshots.Count == 0) return new Dictionary<string, MetricEvolution>();
        try
        {
            var previous = await _measurementRepository.GetLastPerMetricAsync(patientId,
                snapshots.Select(snapshot => snapshot.MetricCode).Distinct().ToArray(), batchId, ct);
            return LabExamEvolutionBuilder.Build(snapshots, previous);
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            // Sin historia disponible no se afirma falsamente que se trata del primer registro.
            _logger.LogWarning(error, "No se pudo consultar el historial para comparar el examen {BatchId}", batchId);
            return new Dictionary<string, MetricEvolution>();
        }
    }

    private static string PendingSummary(int candidateCount, string status, string? language)
    {
        if (language == "en")
            return status == "pending_review"
                ? $"File received and available for review. OCR extracted {candidateCount} possible values that must be checked against the original document. No measurements were added to your clinical history and no program control was completed. Review the file with your care team."
                : "File received and available for review. OCR could not extract readable values. No measurements were added to your clinical history and no program control was completed. Review the original file with your care team.";
        return status == "pending_review"
            ? $"Archivo recibido y disponible para revisión. La extracción OCR detectó {candidateCount} posibles valores que deben verificarse contra el documento original. No se añadieron mediciones a tu historia clínica ni se completó un control del programa. Revisa el archivo con tu equipo de atención."
            : "Archivo recibido y disponible para revisión. La extracción OCR no obtuvo valores legibles. No se añadieron mediciones a tu historia clínica ni se completó un control del programa. Revisa el archivo original con tu equipo de atención.";
    }

    private static void ValidateInput(UploadLabExamCommand request)
    {
        if (request.FileLength <= 0)
        {
            throw new UnprocessableEntityException("The file cannot be empty.");
        }

        if (request.FileLength > UploadLabExamCommandValidator.MaxRawSizeBytes)
        {
            throw new LabExamRawFileTooLargeException();
        }

        var ext = Path.GetExtension(request.FileName);
        var normalizedContentType = request.ContentType?.Split(';')[0].Trim();

        if (
            string.IsNullOrWhiteSpace(ext)
            || !UploadLabExamCommandValidator.AllowedExtensions.Contains(ext)
            || string.IsNullOrWhiteSpace(normalizedContentType)
            || !UploadLabExamCommandValidator.AllowedContentTypes.Contains(normalizedContentType)
        )
        {
            throw new UnsupportedLabExamFileTypeException();
        }
    }

    private static string ResolveExtension(string fileName, string finalContentType)
    {
        var ext = finalContentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "application/pdf" => ".pdf",
            _ => Path.GetExtension(fileName).ToLowerInvariant(),
        };

        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = ".jpg";
        }

        if (!ext.StartsWith('.'))
        {
            ext = "." + ext;
        }

        return ext;
    }

    private static MeasurementMetric? ResolveMetric(
        string rawName,
        IReadOnlyList<MeasurementMetric> activeMetrics
    )
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return null;

        var trimmed = rawName.Trim();

        // 1. Direct alias lookup
        if (MetricAliases.TryGetValue(trimmed, out var canonicalCode))
        {
            var found = activeMetrics.FirstOrDefault(m =>
                string.Equals(m.Code, canonicalCode, StringComparison.OrdinalIgnoreCase)
            );
            if (found is not null)
            {
                return found;
            }
        }

        // 2. Exact match on Code
        var byCode = activeMetrics.FirstOrDefault(m =>
            string.Equals(m.Code, trimmed, StringComparison.OrdinalIgnoreCase)
        );
        if (byCode is not null)
        {
            return byCode;
        }

        // 3. Exact match on Name
        var byName = activeMetrics.FirstOrDefault(m =>
            string.Equals(m.Name, trimmed, StringComparison.OrdinalIgnoreCase)
        );
        if (byName is not null)
        {
            return byName;
        }

        // 4. Normalized match ignoring diacritics and casing
        var normalizedRaw = NormalizeString(trimmed);
        return activeMetrics.FirstOrDefault(m =>
            NormalizeString(m.Code) == normalizedRaw || NormalizeString(m.Name) == normalizedRaw
        );
    }

    private static string NormalizeString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (
                CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark
                && !char.IsPunctuation(c)
            )
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString().Replace(" ", "").Replace("_", "");
    }

}
