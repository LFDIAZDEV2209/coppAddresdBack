using System.Text.Json.Nodes;
using CoppAddresd.Domain.Entities.ProgramProgress;

namespace CoppAddresd.Application.Features.ProgramProgress.Assignment;

/// <summary>Datos clínicos medidos y resultados de las evaluaciones de la batería concreta.</summary>
public sealed record AssignmentProfileData(Guid PatientId, Guid BatteryAssignmentId, string? Sex,
    string? Ethnicity, IReadOnlyList<AssignmentResult> Results, IReadOnlyList<AssignmentBiometric> Biometrics);

public sealed record AssignmentResult(Guid EvaluationId, string TestCode, string Type, string Code,
    decimal Value, string? Severity, string? Qualifier, decimal? Maximum, DateTime? ObservedAt = null);

/// <summary>Conserva pregunta, unidad, valor real y límites publicados; nunca usa el default de la pregunta.</summary>
public sealed record AssignmentBiometric(Guid EvaluationId, string QuestionCode, decimal Value,
    string? Unit, decimal? Minimum, decimal? Maximum, DateTime? ObservedAt = null);

public sealed record AssignmentCatalog(IReadOnlyList<ProgramCondition> Conditions,
    IReadOnlyList<ProgramAssignmentRule> Rules, IReadOnlyList<AssignmentContent> NutritionPlans,
    IReadOnlyList<AssignmentContent> Routines);

public sealed record AssignmentContent(Guid Id, string Name, int? Difficulty = null);
public sealed record AssignmentDecision(string ProfileSnapshot, string Proposal);
