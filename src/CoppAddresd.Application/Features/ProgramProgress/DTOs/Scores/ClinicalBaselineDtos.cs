using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;

/// <summary>
/// Escritura de una línea base clínica (<c>app.clinical_baselines</c>, SPEC
/// §13.1.2): el <c>SetBy</c> es obligatorio y debe resolver a un clínico
/// (AC-22); un paciente jamás se auto-asigna una línea base.
/// </summary>
public sealed record ClinicalBaselineWrite(
    Guid PatientId,
    Guid MetricId,
    decimal Value,
    Guid UnitId,
    FavorableDirection FavorableDirection,
    DateOnly MeasuredAt,
    Guid SetBy,
    decimal? TargetValue);

/// <summary>Línea base clínica para lectura (nunca se expone la entidad).</summary>
public sealed record ClinicalBaselineDto(
    Guid Id,
    Guid PatientId,
    Guid MetricId,
    string MetricCode,
    decimal Value,
    Guid UnitId,
    string UnitSymbol,
    FavorableDirection FavorableDirection,
    decimal? TargetValue,
    DateOnly MeasuredAt,
    Guid SetBy,
    DateTime CreatedAt);