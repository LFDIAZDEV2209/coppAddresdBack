using CoppAddresd.Application.Features.HealthTests;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using MediatR;

namespace CoppAddresd.Application.Features.HealthTests.Alerts;

// --- List Alerts ---

public record ListAlertsQuery(
    Guid? PatientId = null,
    string? Status = null,
    string? Severity = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<PaginatedHealthTestsResult<HealthTestAlertDto>>;

public sealed class ListAlertsQueryHandler(IHealthTestRepository repository)
    : IRequestHandler<ListAlertsQuery, PaginatedHealthTestsResult<HealthTestAlertDto>>
{
    public async Task<PaginatedHealthTestsResult<HealthTestAlertDto>> Handle(
        ListAlertsQuery request,
        CancellationToken ct
    )
    {
        var (items, total) = await repository.ListAlertsAsync(
            request.PatientId,
            request.Status,
            request.Severity,
            Math.Max(1, request.Page),
            Math.Clamp(request.PageSize, 1, 100),
            ct
        );
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return new PaginatedHealthTestsResult<HealthTestAlertDto>(
            items.Select(HealthTestAlertDto.FromEntity).ToList(),
            total,
            Math.Max(1, request.Page),
            pageSize,
            totalPages
        );
    }
}

// --- Transition alert (review / resolve / close) ---

public record TransitionAlertCommand(
    Guid AlertId,
    HealthTestAlertStatus TargetStatus,
    Guid? ActorId = null
) : IRequest<HealthTestAlertDto?>;

public sealed class TransitionAlertCommandHandler(IHealthTestRepository repository)
    : IRequestHandler<TransitionAlertCommand, HealthTestAlertDto?>
{
    public async Task<HealthTestAlertDto?> Handle(
        TransitionAlertCommand request,
        CancellationToken ct
    )
    {
        var alert = await repository.GetAlertByIdAsync(request.AlertId, ct);
        if (alert is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        switch (request.TargetStatus)
        {
            case HealthTestAlertStatus.reviewing:
                alert.Status = HealthTestAlertStatus.reviewing;
                alert.ReviewedBy = request.ActorId;
                alert.ReviewedAt = now;
                break;
            case HealthTestAlertStatus.resolved:
                alert.Status = HealthTestAlertStatus.resolved;
                alert.ResolvedBy = request.ActorId;
                alert.ResolvedAt = now;
                break;
            case HealthTestAlertStatus.closed:
                alert.Status = HealthTestAlertStatus.closed;
                alert.ResolvedBy = request.ActorId;
                alert.ResolvedAt = alert.ResolvedAt ?? now;
                break;
            default:
                throw new InvalidOperationException(
                    $"Transición no soportada: {request.TargetStatus}"
                );
        }

        await repository.UpdateAlertAsync(alert, ct);
        return HealthTestAlertDto.FromEntity(alert);
    }
}

// --- Add Comment ---

public record AddCommentRequest(Guid PatientId, Guid? EvaluationId, string Body);

public record AddCommentCommand(AddCommentRequest Request, Guid AuthorId) : IRequest<Guid>;

public sealed class AddCommentCommandHandler(IHealthTestRepository repository)
    : IRequestHandler<AddCommentCommand, Guid>
{
    public async Task<Guid> Handle(AddCommentCommand request, CancellationToken ct)
    {
        var comment = new HealthTestComment
        {
            Id = Guid.NewGuid(),
            PatientId = request.Request.PatientId,
            EvaluationId = request.Request.EvaluationId,
            AuthorId = request.AuthorId,
            Body = request.Request.Body.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        await repository.AddCommentAsync(comment, ct);
        return comment.Id;
    }
}

// --- Stats (dashboard ERP) ---

public record HealthTestStatsDto(
    int TotalPatients,
    int WithPending,
    int Completed,
    int HighRisk,
    int ActiveAlerts
);

public record GetHealthTestStatsQuery(Guid? ProfessionalId = null) : IRequest<HealthTestStatsDto>;

public sealed class GetHealthTestStatsQueryHandler(IHealthTestRepository repository)
    : IRequestHandler<GetHealthTestStatsQuery, HealthTestStatsDto>
{
    public async Task<HealthTestStatsDto> Handle(
        GetHealthTestStatsQuery request,
        CancellationToken ct
    )
    {
        var patientIds = request.ProfessionalId is { } profId
            ? await repository.GetPatientIdsForProfessionalAsync(profId, ct)
            : null;

        var total = patientIds is null ? await repository.CountPatientsAsync(ct) : patientIds.Count;

        var pending = patientIds is null
            ? await repository.CountAssignmentsByStatusAsync(HealthTestAssignmentStatus.pending, ct)
            : await repository.CountAssignmentsByStatusForPatientsAsync(
                HealthTestAssignmentStatus.pending,
                patientIds,
                ct
            );

        var completed = patientIds is null
            ? await repository.CountAssignmentsByStatusAsync(
                HealthTestAssignmentStatus.completed,
                ct
            )
            : await repository.CountAssignmentsByStatusForPatientsAsync(
                HealthTestAssignmentStatus.completed,
                patientIds,
                ct
            );

        var highRisk = patientIds is null
            ? await repository.CountEvaluationsBySeverityAsync(HealthTestSeverity.high, ct)
            : await repository.CountEvaluationsBySeverityForPatientsAsync(
                HealthTestSeverity.high,
                patientIds,
                ct
            );

        var activeAlerts = patientIds is null
            ? await repository.CountAlertsByStatusAsync(HealthTestAlertStatus.active, ct)
            : await repository.CountAlertsByStatusForPatientsAsync(
                HealthTestAlertStatus.active,
                patientIds,
                ct
            );

        return new HealthTestStatsDto(total, pending, completed, highRisk, activeAlerts);
    }
}
