namespace CoppAddresd.Application.DTOs.Ai;

/// <summary>
/// Contexto clínico consolidado de un paciente para el AI Service. Es el
/// contrato plano que serializa el <see cref="Interfaces.IAiServiceClient"/>
/// en snake_case: la IA recibe el contexto sin referencias a entidades del
/// dominio ni datos sensibles innecesarios.
/// </summary>
public record ClinicalContextDto(
    ClinicalPatientDto Patient,
    IReadOnlyList<ClinicalMeasurementDto> Measurements,
    IReadOnlyList<string> Allergies,
    IReadOnlyList<string> Diagnoses,
    IReadOnlyList<string> Medications,
    ClinicalLifestyleDto Lifestyle);

/// <summary>Datos demográficos y de estilo de vida del paciente para la IA.</summary>
public record ClinicalPatientDto(
    int Age,
    string? Gender,
    string? ExerciseLevel);

/// <summary>
/// Medición clínica consolidada: código de métrica, valor, código de unidad y
/// fecha de observación. La unidad siempre acompaña al valor para que la IA no
/// asuma unidades.
/// </summary>
public record ClinicalMeasurementDto(
    string Metric,
    decimal Value,
    string Unit,
    DateTime ObservedAt);

/// <summary>Estilo de vida declarado por el paciente (historia clínica).</summary>
public record ClinicalLifestyleDto(
    string? Smoking,
    string? Alcohol,
    string? ExerciseLevel);

/// <summary>
/// Restricción de seguridad aplicable al plan: texto legible de la regla y
/// severidad (<c>block</c> | <c>warning</c>).
/// </summary>
public record RestrictionDto(
    string Rule,
    string Severity);