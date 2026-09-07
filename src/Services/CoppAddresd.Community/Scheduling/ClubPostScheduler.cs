using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Persistence;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.Scheduling;

/// <summary>
/// Scheduler del servicio de comunidad: cada 30 segundos publica los posts de
/// clubes que estaban PROGRAMADO y cuya fecha ya venció (pasa a Publicado y
/// emite la suscripción club_post_added). Idempotente y a prueba de reinicios
/// (consulta por estado + fecha, no por memoria).
/// </summary>
public sealed class ClubPostScheduler(
    IServiceScopeFactory scopeFactory,
    ITopicEventSender sender,
    ILogger<ClubPostScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PublishDuePostsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publicando posts programados de clubes.");
            }
        }
    }

    private async Task PublishDuePostsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var scope = scopeFactory.CreateScope();
        using var _ = scope;
        var db = scope.ServiceProvider.GetRequiredService<CommunityDbContext>();

        var due = await db.Posts
            .Where(p => p.ClubId != null
                && p.ClubStatus == ClubPostStatus.Programado
                && p.ScheduledFor != null
                && p.ScheduledFor <= now
                && p.DeletedAt == null)
            .ToListAsync(ct);

        if (due.Count == 0) return;

        foreach (var post in due)
        {
            post.ClubStatus = ClubPostStatus.Publicado;
            post.ScheduledFor = null;
            post.UpdatedAt = now;
            await sender.SendAsync($"club_{post.ClubId}_posts", post, ct);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Publicados {Count} posts programados de clubes.", due.Count);
    }
}