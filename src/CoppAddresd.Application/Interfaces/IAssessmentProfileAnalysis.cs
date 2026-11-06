namespace CoppAddresd.Application.Interfaces;

public sealed record AssessmentGuidancePriority(string Title, string Explanation, string Action, IReadOnlyList<string> EvidenceCodes);
public sealed record AssessmentGuidanceSource(string EvidenceCode, string TestCode, string TestName,
    string Code, string Label, string ResultType, decimal Value, string? Qualifier,
    string? Severity, DateTime? CompletedAt);
public sealed record AssessmentProfileGuidance(string Summary, IReadOnlyList<AssessmentGuidancePriority> Priorities,
    string Encouragement, IReadOnlyList<string> Questions, DateTime GeneratedAt, string SourceRevision,
    IReadOnlyList<AssessmentGuidanceSource> Sources, bool IsPartial);

public interface IAssessmentProfileAnalysis
{
    Task<AssessmentProfileGuidance?> GetAsync(Guid patientId, Guid userId, string language, bool generate, CancellationToken ct);
}

public sealed class AssessmentAnalysisUnavailableException : Exception
{
    public AssessmentAnalysisUnavailableException() : base("No fue posible generar el análisis de tus evaluaciones.") { }
}
