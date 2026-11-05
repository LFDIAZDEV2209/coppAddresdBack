using System.Text.Json;

namespace CoppAddresd.Application.Features.ProgramProgress.Assignment;

public sealed record AssignmentEnrollmentDto(Guid Id, string Status, int CurrentWeekNumber, DateOnly StartLocalDate,
    DateTime StartedAt, string Timezone);
public sealed record AssignmentFallbackInput(Guid RecommendationId, DateTime UtcNow);
public sealed record AppliedAssignmentDto(Guid RecommendationId, Guid EnrollmentId, Guid PatientId, string State,
    DateOnly StartLocalDate, int CurrentWeekNumber, JsonElement AppliedContent, bool Replay, AssignmentEnrollmentDto? Enrollment = null);
public sealed record AssignmentRecoveryCandidate(Guid PatientId, Guid BatteryAssignmentId);
public sealed record AssignmentFallbackCandidate(Guid RecommendationId, DateTime CreatedAt);
public sealed record ProgramAssignmentJobResult(int Recovered, int Fallbacks, int Notifications);
public sealed record AssignmentInboxItem(Guid Id, Guid RecommendationId, string Kind, string TitleEs, string TitleEn,
    string MessageEs, string MessageEn, string Link, DateTime CreatedAt, DateTime? ReadAt);
public sealed record AssignmentInboxPage(IReadOnlyList<AssignmentInboxItem> Items, int Total, int Page, int PageSize);
