using System.Text.Json;

namespace CoppAddresd.Application.Features.ProgramProgress.Assignment;

public sealed record AssignmentQueueItem(Guid Id, Guid EnrollmentId, Guid PatientId, string PatientName,
    string? DocumentNumber, string State, DateTime CreatedAt, int WaitingDays, int MaxSeverity, bool NarrativePending);
public sealed record AssignmentQueuePage(IReadOnlyList<AssignmentQueueItem> Items, int Total, int Page, int PageSize, int TotalPages);
public sealed record AssignmentEnrollmentDto(Guid Id, string Status, int CurrentWeekNumber, DateOnly StartLocalDate,
    DateTime StartedAt, string Timezone);
public sealed record AssignmentDetailDto(Guid Id, Guid PatientId, string PatientName, string? DocumentNumber,
    AssignmentEnrollmentDto Enrollment, JsonElement ProfileSnapshot, JsonElement Proposal,
    string? NarrativeEs, string? NarrativeEn, bool NarrativePending, string State, string EngineVersion, string? ModelName,
    Guid? AppliedBy, Guid? DismissedBy, DateTime CreatedAt, DateTime? UpdatedAt, DateTime? AppliedAt, DateTime? DismissedAt);
public sealed record AssignmentActivationInput(Guid RecommendationId, string? OverrideNutritionPlanName,
    string? OverrideRoutineName, Guid? ActorId, IReadOnlyList<Guid>? ScopedPatientIds, DateTime UtcNow, bool UseTemplateDefaults = false);
public sealed record AppliedAssignmentDto(Guid RecommendationId, Guid EnrollmentId, Guid PatientId, string State,
    DateOnly StartLocalDate, int CurrentWeekNumber, JsonElement AppliedContent, bool Replay, AssignmentEnrollmentDto? Enrollment = null);
public sealed record AssignmentRecoveryCandidate(Guid PatientId, Guid BatteryAssignmentId);
public sealed record AssignmentFallbackCandidate(Guid RecommendationId, DateTime CreatedAt);
public sealed record ProgramAssignmentJobResult(int Recovered, int Fallbacks, int Notifications);
public sealed record AssignmentInboxItem(Guid Id, Guid RecommendationId, string Kind, string TitleEs, string TitleEn,
    string MessageEs, string MessageEn, string Link, DateTime CreatedAt, DateTime? ReadAt);
public sealed record AssignmentInboxPage(IReadOnlyList<AssignmentInboxItem> Items, int Total, int Page, int PageSize);
public sealed record AssignmentCacheRevision(string Token);
