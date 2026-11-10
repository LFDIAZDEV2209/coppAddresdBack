using System.Runtime.CompilerServices;
using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Persistence;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.Security;

/// <summary>
/// Reevalúa la visibilidad con un contexto nuevo por evento. Solo se conserva la
/// identidad del suscriptor; los bloqueos y las membresías siempre se leen de la BD.
/// </summary>
internal static class BlockingEventStream
{
    public static async ValueTask<ISourceStream<T>> SubscribeAsync<T>(
        ITopicEventReceiver receiver,
        string topic,
        IServiceScopeFactory scopeFactory,
        Guid? viewerProfileId,
        Func<T, CommunityDbContext, CancellationToken, Task<T?>> reload,
        CancellationToken ct) where T : class
    {
        var source = await receiver.SubscribeAsync<T>(topic, ct);
        return new FilteredSourceStream<T>(source, scopeFactory, viewerProfileId, reload, ct);
    }

    /// <summary>El bypass de moderación exige audiencia ERP y permiso explícito.</summary>
    public static bool IsErpModerator(IHttpContextAccessor http)
        => http.HttpContext?.User is { Identity.IsAuthenticated: true } user
           && user.HasClaim("aud", "erp") && !user.HasClaim("aud", "app")
           && user.Claims.Any(c => c.Type == "permission"
               && (c.Value.Equals("Community.Manage", StringComparison.OrdinalIgnoreCase)
                   || c.Value.Equals("Community.Moderate", StringComparison.OrdinalIgnoreCase)
                   || c.Value.Equals("Community.View", StringComparison.OrdinalIgnoreCase)));

    public static Task<bool> CanViewClubAsync(
        CommunityDbContext db, Guid clubId, Guid? viewerProfileId, bool moderator,
        bool requireMembership, CancellationToken ct)
        => db.Clubs.AnyAsync(c => c.Id == clubId && (moderator ||
            (c.Status == ClubStatus.Activo && ((!requireMembership && c.Visibility == ClubVisibility.Publico)
             || db.ClubMembers.Any(m => m.ClubId == c.Id && m.ProfileId == viewerProfileId
                 && m.Status == ClubMemberStatus.Activo)))), ct);

    public static async Task<bool> CanViewPostAsync(
        CommunityDbContext db, Post post, Guid? viewerProfileId, bool moderator, CancellationToken ct)
    {
        if (post.ClubId is not { } clubId)
            return true;
        if (moderator)
            return await CanViewClubAsync(db, clubId, viewerProfileId, moderator, false, ct);
        return post.ClubStatus == ClubPostStatus.Publicado
            && await CanViewClubAsync(db, clubId, viewerProfileId, false,
                post.ClubVisibility != ClubPostVisibility.Publico, ct);
    }

    private sealed class FilteredSourceStream<T>(
        ISourceStream<T> source,
        IServiceScopeFactory scopeFactory,
        Guid? viewerProfileId,
        Func<T, CommunityDbContext, CancellationToken, Task<T?>> reload,
        CancellationToken subscriptionCancellation) : ISourceStream<T> where T : class
    {
        private readonly CancellationTokenSource lifetime =
            CancellationTokenSource.CreateLinkedTokenSource(subscriptionCancellation);
        private int disposed;

        public IAsyncEnumerable<T> ReadEventsAsync() => ReadFilteredEventsAsync();

        IAsyncEnumerable<object?> ISourceStream.ReadEventsAsync() => ReadUntypedEventsAsync();

        private async IAsyncEnumerable<object?> ReadUntypedEventsAsync(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var item in ReadFilteredEventsAsync().WithCancellation(ct).ConfigureAwait(false))
                yield return item;
        }

        private async IAsyncEnumerable<T> ReadFilteredEventsAsync(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token);
            await foreach (var queued in source.ReadEventsAsync().WithCancellation(cancellation.Token).ConfigureAwait(false))
            {
                cancellation.Token.ThrowIfCancellationRequested();
                T? visible;
                await using (var scope = scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<CommunityDbContext>();
                    db.UseBlockVisibility(viewerProfileId);
                    db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                    visible = await reload(queued, db, cancellation.Token).ConfigureAwait(false);
                }
                // Un campo GraphQL no nulo no admite emitir null: se omite el evento completo.
                if (visible is not null)
                    yield return visible;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;
            try
            {
                await lifetime.CancelAsync().ConfigureAwait(false);
                await source.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                lifetime.Dispose();
            }
        }
    }
}
