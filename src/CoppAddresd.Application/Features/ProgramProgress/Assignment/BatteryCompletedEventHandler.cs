using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.HealthTests.Events;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.ProgramProgress;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Application.Features.ProgramProgress.Assignment;

/// <summary>Consumidor best-effort posterior al commit clínico; inscripción y propuesta se confirman juntas.</summary>
public sealed class BatteryCompletedEventHandler(IProgramAssignmentRepository assignments, IProgramRepository programs,
    AssignmentEngine engine, IOptionsMonitor<ProgramAssignmentSettings> settings, IConfiguration configuration,
    IProgramNarrativeQueue narratives, ICacheService cache, ILogger<BatteryCompletedEventHandler> logger, IProgramAssignmentNoticeRepository? notices = null, TimeProvider? clock = null)
    : INotificationHandler<BatteryCompletedEvent>
{
    public async Task Handle(BatteryCompletedEvent notification, CancellationToken ct)
    {
        if (!settings.CurrentValue.Enabled) return;
        Guid? recommendationId = null;
        try
        {
            await assignments.ExecuteForPatientAsync(notification.PatientId, async () =>
            {
                var pending = await assignments.FindPendingWithoutRecommendationAsync(notification.PatientId, ct);
                if ((pending is null && await assignments.HasCurrentEnrollmentAsync(notification.PatientId, ct))
                    || await assignments.FindCompletedInitialBatteryAsync(notification.PatientId, notification.BatteryAssignmentId, ct) is null)
                    return;
                var profile = await assignments.ReadProfileAsync(notification.PatientId, notification.BatteryAssignmentId, ct);
                var catalog = await assignments.ReadCatalogAsync(ct);
                var decision = engine.Build(profile, catalog, settings.CurrentValue, (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime);
                var enrollment = pending ?? await programs.CreatePendingAssignmentAsync(notification.PatientId,
                    configuration["Program:DefaultTemplate:Code"] ?? "program-coppaddresd-83-days", ct);
                var recommendation = new ProgramAssignmentRecommendation
                {
                    Id = Guid.NewGuid(), EnrollmentId = enrollment.Id, PatientId = notification.PatientId,
                    ProfileSnapshot = decision.ProfileSnapshot, Proposal = decision.Proposal,
                    State = "proposed", EngineVersion = AssignmentEngine.Version,
                    CreatedAt = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime,
                };
                await assignments.AddRecommendationAsync(recommendation, ct);
                if (notices is not null) await notices.AddProfessionalNoticesAsync(recommendation.Id,notification.PatientId,recommendation.CreatedAt,ct);
                recommendationId = recommendation.Id;
            }, ct);
            if (recommendationId.HasValue)
            {
                await cache.SetAsync("program-assignment-revision:v1", new AssignmentCacheRevision(Guid.NewGuid().ToString("N")), TimeSpan.FromDays(1), ct);
                await cache.RemoveAsync(CacheKeys.ScoresHistory(notification.PatientId), ct);
                if (!narratives.TryEnqueue(recommendationId.Value))
                    logger.LogWarning("Program.NarrativeQueueFull RecommendationId={RecommendationId}", recommendationId);
            }
        }
        catch (Exception ex)
        {
            // También se conserva el submit si la request se cancela tras su COMMIT.
            logger.LogWarning(ex, "Program.BatteryAssignmentFailed BatteryAssignmentId={BatteryAssignmentId}", notification.BatteryAssignmentId);
        }
    }
}
