using System.Globalization;
using System.Text;
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
/// validación → compresión → almacenamiento en S3 → extracción de métricas con IA
/// → resolución contra catálogo → persistencia en lote.
/// Ante cualquier fallo posterior al almacenamiento en S3, se ejecuta compensación (DeleteObjectAsync).
/// </summary>
public class UploadLabExamCommandHandler : IRequestHandler<UploadLabExamCommand, LabExamUploadResult>
{
    private static readonly Dictionary<string, string> MetricAliases = new(StringComparer.OrdinalIgnoreCase)
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
    private readonly ILogger<UploadLabExamCommandHandler> _logger;

    public UploadLabExamCommandHandler(
        IFileCompressionService compressionService,
        IObjectStorageService storageService,
        IAiServiceClient aiServiceClient,
        IClinicalMeasurementRepository measurementRepository,
        IPatientRepository patientRepository,
        ILogger<UploadLabExamCommandHandler> logger)
    {
        _compressionService = compressionService;
        _storageService = storageService;
        _aiServiceClient = aiServiceClient;
        _measurementRepository = measurementRepository;
        _patientRepository = patientRepository;
        _logger = logger;
    }

    public async Task<LabExamUploadResult> Handle(UploadLabExamCommand request, CancellationToken ct)
    {
        ValidateInput(request);

        // Resolve patient profile (request.PatientId could be PatientProfile.Id or auth.users.id from JWT)
        var patientProfile = await _patientRepository.GetByIdAsync(request.PatientId, ct)
                             ?? await _patientRepository.GetByUserIdAsync(request.PatientId, ct);

        if (patientProfile is null)
        {
            _logger.LogWarning("Patient profile not found for identifier '{Identifier}'", request.PatientId);
            throw new NotFoundException($"Patient profile not found for identifier '{request.PatientId}'.");
        }

        var patientId = patientProfile.Id;

        // 1. Generate batchId
        var batchId = Guid.NewGuid();

        // 2. Compress file
        var (compressedStream, finalContentType) = await _compressionService.CompressAsync(
            request.FileStream, request.ContentType, ct);

        // 3. Construct S3 storage key: lab-exams/{patientId}/{batchId}{extension}
        var extension = ResolveExtension(request.FileName, finalContentType);
        var storageKey = $"lab-exams/{patientId}/{batchId}{extension}";

        // 4. Upload to S3
        if (compressedStream.CanSeek && compressedStream.Position != 0)
        {
            compressedStream.Position = 0;
        }

        await _storageService.PutObjectAsync(storageKey, compressedStream, finalContentType, ct);

        // 5. Extracción + persistencia en try/catch (compensación S3 si falla)
        LabExamAiResponse aiResponse;
        var measurements = new List<ClinicalMeasurement>();
        var detectedMetricNames = new List<string>();
        var detectedMetricCodes = new List<string>();
        var currentSnapshots = new List<LabExamMetricSnapshot>();

        try
        {
            if (compressedStream.CanSeek && compressedStream.Position != 0)
            {
                compressedStream.Position = 0;
            }

            var fileNameForAi = Path.GetFileNameWithoutExtension(request.FileName) + extension;

            aiResponse = await _aiServiceClient.ExtractLabMetricsAsync(
                patientId,
                batchId,
                compressedStream,
                fileNameForAi,
                finalContentType,
                request.ThreadId,
                ct);

            // b. If !response.Readable or zero metrics: return result with AI summary (no measurements saved)
            if (!aiResponse.Readable || aiResponse.Metrics.Count == 0)
            {
                return new LabExamUploadResult(
                    batchId,
                    aiResponse.Summary,
                    0,
                    Array.Empty<string>(),
                    storageKey);
            }

            // c. Resolve metric codes/aliases against active MeasurementMetric catalog
            var activeMetrics = await _measurementRepository.GetActiveMetricsWithUnitsAsync(ct);
            var activeUnits = await _measurementRepository.GetActiveUnitsAsync(ct);

            foreach (var aiMetric in aiResponse.Metrics)
            {
                var matchedMetric = ResolveMetric(aiMetric.MetricName, activeMetrics);
                if (matchedMetric is null)
                {
                    _logger.LogWarning(
                        "Lab exam metric '{MetricName}' could not be matched to catalog. Skipping.",
                        aiMetric.MetricName);
                    continue;
                }

                var unit = ResolveUnit(aiMetric.UnitSymbol, matchedMetric, activeUnits) ?? matchedMetric.DefaultUnit;
                var observedAt = aiMetric.ObservedAt.HasValue
                    ? aiMetric.ObservedAt.Value.ToUniversalTime()
                    : DateTime.UtcNow;

                measurements.Add(new ClinicalMeasurement
                {
                    Id = Guid.NewGuid(),
                    PatientId = patientId,
                    MetricId = matchedMetric.Id,
                    UnitId = unit?.Id ?? matchedMetric.DefaultUnitId,
                    Value = aiMetric.Value,
                    ObservedAt = observedAt,
                    RecordedAt = DateTime.UtcNow,
                    Source = "lab",
                    BatchId = batchId,
                    SourceKey = storageKey,
                    CreatedBy = request.PatientId,
                    CreatedAt = DateTime.UtcNow
                });

                detectedMetricNames.Add(matchedMetric.Name);
                detectedMetricCodes.Add(matchedMetric.Code);
                currentSnapshots.Add(new LabExamMetricSnapshot(
                    matchedMetric.Code, aiMetric.Value, unit?.Symbol, observedAt));
            }

            if (measurements.Count > 0)
            {
                await _measurementRepository.AddBatchAsync(measurements, ct);
            }
        }
        catch (Exception ex)
        {
            // 6. Compensation: Delete S3 object to guarantee atomicity
            _logger.LogWarning(ex,
                "Failed to process lab exam for patient {PatientId}. Storage key {StorageKey} was deleted as compensation.",
                patientId, storageKey);

            try
            {
                await _storageService.DeleteObjectAsync(storageKey, CancellationToken.None);
            }
            catch (Exception delEx)
            {
                _logger.LogError(delEx,
                    "Failed to delete S3 object {StorageKey} during compensation for patient {PatientId}.",
                    storageKey, request.PatientId);
            }

            throw;
        }

        // 7. Narración empática: DESPUÉS de la persistencia y FUERA del try de
        // compensación — un fallo de narración jamás alcanza DeleteObjectAsync (R12).
        var summary = aiResponse.Summary;
        if (measurements.Count > 0)
        {
            var empathetic = await BuildNarrationAsync(
                patientId, batchId, aiResponse, currentSnapshots, detectedMetricCodes, request.Language, ct);
            if (!string.IsNullOrWhiteSpace(empathetic))
            {
                summary = empathetic;
            }
        }

        return new LabExamUploadResult(
            batchId,
            summary,
            measurements.Count,
            detectedMetricNames,
            storageKey);
    }

    /// <summary>
    /// Narración best-effort (R7/R8/R11/R12): query de historia → deltas en C# →
    /// llamada al ai-service. Cualquier fallo (incluida la query de contexto)
    /// degrada a null y el upload continúa con el summary técnico; nunca se
    /// persiste el mensaje empático (R9).
    /// </summary>
    private async Task<string?> BuildNarrationAsync(
        Guid patientId,
        Guid batchId,
        LabExamAiResponse aiResponse,
        IReadOnlyList<LabExamMetricSnapshot> currentSnapshots,
        IReadOnlyList<string> detectedMetricCodes,
        string? language,
        CancellationToken ct)
    {
        try
        {
            var previous = await _measurementRepository.GetLastPerMetricAsync(
                patientId, detectedMetricCodes, batchId, ct);

            var evolutions = LabExamEvolutionBuilder.Build(currentSnapshots, previous);

            return await _aiServiceClient.NarrateLabExamAsync(
                patientId, batchId, aiResponse.Metrics, evolutions, language, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "Lab exam narration context failed for patient {PatientId} batch {BatchId}. Falling back to summary.",
                patientId, batchId);
            return null;
        }
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

        if (string.IsNullOrWhiteSpace(ext) || !UploadLabExamCommandValidator.AllowedExtensions.Contains(ext) ||
            string.IsNullOrWhiteSpace(normalizedContentType) || !UploadLabExamCommandValidator.AllowedContentTypes.Contains(normalizedContentType))
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
            _ => Path.GetExtension(fileName).ToLowerInvariant()
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
        IReadOnlyList<MeasurementMetric> activeMetrics)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return null;

        var trimmed = rawName.Trim();

        // 1. Direct alias lookup
        if (MetricAliases.TryGetValue(trimmed, out var canonicalCode))
        {
            var found = activeMetrics.FirstOrDefault(m =>
                string.Equals(m.Code, canonicalCode, StringComparison.OrdinalIgnoreCase));
            if (found is not null)
            {
                return found;
            }
        }

        // 2. Exact match on Code
        var byCode = activeMetrics.FirstOrDefault(m =>
            string.Equals(m.Code, trimmed, StringComparison.OrdinalIgnoreCase));
        if (byCode is not null)
        {
            return byCode;
        }

        // 3. Exact match on Name
        var byName = activeMetrics.FirstOrDefault(m =>
            string.Equals(m.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (byName is not null)
        {
            return byName;
        }

        // 4. Normalized match ignoring diacritics and casing
        var normalizedRaw = NormalizeString(trimmed);
        return activeMetrics.FirstOrDefault(m =>
            NormalizeString(m.Code) == normalizedRaw || NormalizeString(m.Name) == normalizedRaw);
    }

    private static string NormalizeString(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && !char.IsPunctuation(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString().Replace(" ", "").Replace("_", "");
    }

    private static UnitOfMeasure? ResolveUnit(
        string? unitSymbol,
        MeasurementMetric metric,
        IReadOnlyList<UnitOfMeasure> activeUnits)
    {
        if (string.IsNullOrWhiteSpace(unitSymbol))
        {
            return activeUnits.FirstOrDefault(u => u.Id == metric.DefaultUnitId) ?? metric.DefaultUnit;
        }

        var trimmed = unitSymbol.Trim();
        var normalizedSymbol = NormalizeUnitSymbol(trimmed);

        var matched = activeUnits.FirstOrDefault(u =>
            string.Equals(u.Symbol, normalizedSymbol, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(u.Symbol, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(u.Code, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(u.Name, trimmed, StringComparison.OrdinalIgnoreCase));

        return matched ?? activeUnits.FirstOrDefault(u => u.Id == metric.DefaultUnitId) ?? metric.DefaultUnit;
    }

    private static string NormalizeUnitSymbol(string trimmedUnitSymbol) => trimmedUnitSymbol.ToLowerInvariant() switch
    {
        "%" or "pct" or "porcentaje" => "%",
        "c" or "°c" or "celsius" or "grados celsius" => "°C",
        "bpm" or "lpm" or "latidos/min" or "latidos por minuto" => "bpm",
        "mg/dl" or "mg_dl" => "mg/dL",
        "mmhg" => "mmHg",
        "kg/m2" or "kg/m²" or "kg_m2" => "kg/m²",
        "kg" or "kilos" or "kilogramos" => "kg",
        "cm" or "centimetros" or "centímetros" => "cm",
        _ => trimmedUnitSymbol
    };
}
