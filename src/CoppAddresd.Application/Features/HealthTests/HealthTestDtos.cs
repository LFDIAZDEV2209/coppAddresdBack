using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Features.HealthTests;

// --- Catálogo: instrumentos y versiones ---

public record HealthTestInstrumentDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? Category,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<HealthTestVersionDto> Versions
)
{
    public static HealthTestInstrumentDto FromEntity(HealthTestInstrument i) =>
        new(
            i.Id,
            i.Code,
            i.Name,
            i.Description,
            i.Category,
            i.SortOrder,
            i.IsActive,
            i.CreatedAt,
            i.Versions.OrderByDescending(v => v.VersionNumber)
                .Select(HealthTestVersionDto.FromEntity)
                .ToList()
        );
}

public record HealthTestVersionDto(
    Guid Id,
    Guid InstrumentId,
    int VersionNumber,
    string? Name,
    HealthTestVersionStatus Status,
    bool IsCurrent,
    HealthTestScoringStrategy ScoringStrategy,
    int? Points,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    DateTime? RetiredAt
)
{
    public static HealthTestVersionDto FromEntity(HealthTestVersion v) =>
        new(
            v.Id,
            v.InstrumentId,
            v.VersionNumber,
            v.Name,
            v.Status,
            v.IsCurrent,
            v.ScoringStrategy,
            v.Points,
            v.CreatedAt,
            v.PublishedAt,
            v.RetiredAt
        );
}

public record HealthTestVersionDetailDto(
    Guid Id,
    Guid InstrumentId,
    int VersionNumber,
    string? Name,
    HealthTestVersionStatus Status,
    bool IsCurrent,
    HealthTestScoringStrategy ScoringStrategy,
    int? Points,
    IReadOnlyList<HealthTestQuestionDto> Questions,
    IReadOnlyList<HealthTestScoreRangeDto> Ranges
)
{
    public static HealthTestVersionDetailDto FromEntity(HealthTestVersion v) =>
        new(
            v.Id,
            v.InstrumentId,
            v.VersionNumber,
            v.Name,
            v.Status,
            v.IsCurrent,
            v.ScoringStrategy,
            v.Points,
            v.Questions.OrderBy(q => q.SortOrder).Select(HealthTestQuestionDto.FromEntity).ToList(),
            v.ScoreRanges.OrderBy(r => r.MinValue)
                .Select(HealthTestScoreRangeDto.FromEntity)
                .ToList()
        );
}

public record HealthTestQuestionDto(
    Guid Id,
    Guid VersionId,
    string Code,
    string? Section,
    string Text,
    HealthTestQuestionType Type,
    HealthTestScoringDirection ScoringDirection,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<HealthTestAnswerOptionDto> Options
)
{
    public static HealthTestQuestionDto FromEntity(HealthTestQuestion q) =>
        new(
            q.Id,
            q.VersionId,
            q.Code,
            q.Section,
            q.Text,
            q.Type,
            q.ScoringDirection,
            q.SortOrder,
            q.IsActive,
            q.Options.OrderBy(o => o.SortOrder)
                .Select(HealthTestAnswerOptionDto.FromEntity)
                .ToList()
        );
}

public record HealthTestAnswerOptionDto(
    Guid Id,
    Guid QuestionId,
    string Text,
    decimal? ScoreValue,
    int SortOrder,
    bool IsActive
)
{
    public static HealthTestAnswerOptionDto FromEntity(HealthTestAnswerOption o) =>
        new(o.Id, o.QuestionId, o.Text, o.ScoreValue, o.SortOrder, o.IsActive);
}

public record HealthTestScoreRangeDto(
    Guid Id,
    Guid VersionId,
    decimal MinValue,
    decimal MaxValue,
    string Label,
    HealthTestSeverity Severity,
    bool IsActive
)
{
    public static HealthTestScoreRangeDto FromEntity(HealthTestScoreRange r) =>
        new(r.Id, r.VersionId, r.MinValue, r.MaxValue, r.Label, r.Severity, r.IsActive);
}

// --- Baterías ---

public record HealthTestBatteryDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool AutoAssignOnPatientCreate,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<HealthTestBatteryItemDto> Items
)
{
    public static HealthTestBatteryDto FromEntity(HealthTestBattery b) =>
        new(
            b.Id,
            b.Code,
            b.Name,
            b.Description,
            b.AutoAssignOnPatientCreate,
            b.IsActive,
            b.CreatedAt,
            b.Items.OrderBy(i => i.SortOrder).Select(HealthTestBatteryItemDto.FromEntity).ToList()
        );
}

public record HealthTestBatteryItemDto(
    Guid Id,
    Guid BatteryId,
    Guid InstrumentId,
    Guid? VersionId,
    string? InstrumentName,
    int SortOrder,
    bool IsRequired,
    int? FrequencyDays
)
{
    public static HealthTestBatteryItemDto FromEntity(HealthTestBatteryItem i) =>
        new(
            i.Id,
            i.BatteryId,
            i.InstrumentId,
            i.VersionId,
            i.Instrument?.Name,
            i.SortOrder,
            i.IsRequired,
            i.FrequencyDays
        );
}

// --- Asignaciones ---

public record HealthTestAssignmentDto(
    Guid Id,
    Guid PatientId,
    string? PatientName,
    Guid VersionId,
    string? TestName,
    string? TestCode,
    string? TestCategory,
    Guid? BatteryAssignmentId,
    HealthTestAssignmentStatus Status,
    int? Priority,
    DateTime AssignedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime? ExpiresAt,
    DateTime? DueDate,
    string? Notes
)
{
    public static HealthTestAssignmentDto FromEntity(HealthTestAssignment a) =>
        new(
            a.Id,
            a.PatientId,
            a.Patient is null ? null : $"{a.Patient.FirstName} {a.Patient.LastName}".Trim(),
            a.VersionId,
            // El nombre del test vive en el INSTRUMENTO (la versión snapshot puede no
            // tener nombre propio, como el seed ANTARES). Fallback a Version.Name.
            a.Version?.Instrument?.Name
                ?? a.Version?.Name,
            a.Version?.Instrument?.Code,
            a.Version?.Instrument?.Category,
            a.BatteryAssignmentId,
            a.Status,
            a.Priority,
            a.AssignedAt,
            a.StartedAt,
            a.CompletedAt,
            a.ExpiresAt,
            a.DueDate,
            a.Notes
        );
}

public record HealthTestBatteryAssignmentDto(
    Guid Id,
    Guid PatientId,
    Guid BatteryId,
    string? BatteryName,
    HealthTestAssignmentStatus Status,
    DateTime AssignedAt,
    DateTime? DueDate,
    DateTime? CompletedAt
)
{
    public static HealthTestBatteryAssignmentDto FromEntity(HealthTestBatteryAssignment a) =>
        new(
            a.Id,
            a.PatientId,
            a.BatteryId,
            a.Battery?.Name,
            a.Status,
            a.AssignedAt,
            a.DueDate,
            a.CompletedAt
        );
}

// --- Evaluaciones y resultados ---

public record HealthTestEvaluationDto(
    Guid Id,
    Guid AssignmentId,
    Guid PatientId,
    Guid VersionId,
    string? TestName,
    string? TestCode,
    string? TestCategory,
    HealthTestEvaluationStatus Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    decimal? Score,
    decimal? ScorePercentage,
    IReadOnlyList<HealthTestResultDto> Results
)
{
    public static HealthTestEvaluationDto FromEntity(HealthTestEvaluation e) =>
        new(
            e.Id,
            e.AssignmentId,
            e.PatientId,
            e.VersionId,
            e.Version?.Instrument?.Name ?? e.Version?.Name,
            e.Version?.Instrument?.Code,
            e.Version?.Instrument?.Category,
            e.Status,
            e.StartedAt,
            e.CompletedAt,
            e.Score,
            e.ScorePercentage,
            e.Results.OrderBy(r => r.Code).Select(HealthTestResultDto.FromEntity).ToList()
        );
}

public record HealthTestResultDto(
    Guid Id,
    Guid EvaluationId,
    HealthTestResultType ResultType,
    string Code,
    string Label,
    decimal Value,
    string? Qualifier,
    HealthTestSeverity? Severity
)
{
    public static HealthTestResultDto FromEntity(HealthTestResult r) =>
        new(r.Id, r.EvaluationId, r.ResultType, r.Code, r.Label, r.Value, r.Qualifier, r.Severity);
}

// --- Alertas ---

public record HealthTestAlertDto(
    Guid Id,
    Guid PatientId,
    string? PatientName,
    Guid? ResultId,
    Guid? RuleId,
    string? RuleName,
    HealthTestSeverity Severity,
    string Title,
    string? Body,
    HealthTestAlertStatus Status,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    DateTime? ResolvedAt
)
{
    public static HealthTestAlertDto FromEntity(HealthTestAlert a) =>
        new(
            a.Id,
            a.PatientId,
            a.Patient is null ? null : $"{a.Patient.FirstName} {a.Patient.LastName}".Trim(),
            a.ResultId,
            a.RuleId,
            a.Rule?.Name,
            a.Severity,
            a.Title,
            a.Body,
            a.Status,
            a.CreatedAt,
            a.ReviewedAt,
            a.ResolvedAt
        );
}

// --- Resultados paginados ---

public record PaginatedHealthTestsResult<T>(
    IReadOnlyList<T> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);
