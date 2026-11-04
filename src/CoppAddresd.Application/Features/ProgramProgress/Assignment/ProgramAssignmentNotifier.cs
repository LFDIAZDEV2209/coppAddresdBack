using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Notifications;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Application.Features.ProgramProgress.Assignment;

/// <summary>Reutiliza el fan-out de tokens y el chat proactivo de UC-001 con progreso separado de cada canal.</summary>
public sealed class ProgramAssignmentNotifier(IProgramAssignmentNoticeRepository repository, ISender sender,
    IOptionsMonitor<ProgramAssignmentSettings> settings, ILogger<ProgramAssignmentNotifier> logger) : IProgramAssignmentNotifier
{
    public async Task<int> DispatchAsync(Guid? recommendationId, DateTime utcNow, CancellationToken ct)
    {
        var sent=0;
        foreach (var id in await repository.ListPendingAsync(recommendationId,utcNow,settings.CurrentValue.JobBatchSize,ct))
        {
            var notice=await repository.TryClaimAsync(id,utcNow,ct);
            if (notice is null) continue;
            var push=notice.PushCompleted; var chat=notice.ChatCompleted;
            try
            {
                var english=string.Equals(settings.CurrentValue.NotificationLanguage,"en",StringComparison.OrdinalIgnoreCase);
                var result=await sender.Send(new SendPushNotificationCommand(notice.UserId,
                    english ? notice.TitleEn : notice.TitleEs, english ? notice.MessageEn : notice.MessageEs,
                    settings.CurrentValue.NotificationAgentTypeId, SkipPush:push, SkipChat:chat,
                    Data:new Dictionary<string,string> { ["event_type"]="program-ready",["route"]=notice.Link,
                        ["recommendation_id"]=notice.RecommendationId.ToString(),["body_es"]=notice.MessageEs,["body_en"]=notice.MessageEn }),ct);
                push=push || (result.FailedCount==0 && result.DisabledCount==0);
                chat=chat || !string.IsNullOrWhiteSpace(result.ThreadId);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogWarning(ex,"Program.ReadyNoticeFailed NoticeId={NoticeId}",id); }
            await repository.FinishAsync(id,push,chat,utcNow,ct);
            if (push && chat) sent++;
        }
        return sent;
    }
}
