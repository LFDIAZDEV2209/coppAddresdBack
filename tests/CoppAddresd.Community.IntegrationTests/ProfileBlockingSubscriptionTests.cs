using CoppAddresd.Community.Entities;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.IntegrationTests;

/// <summary>Un socket abierto antes del bloqueo debe reevaluar cada evento con el estado actual.</summary>
[Collection(CommunityTestCollection.Name)]
public sealed class ProfileBlockingSubscriptionTests(CommunityTestDatabase fixture)
{
    [Theory]
    [InlineData("post")]
    [InlineData("comment")]
    [InlineData("nested-comment")]
    [InlineData("feed")]
    [InlineData("group")]
    [InlineData("group-changed")]
    public async Task ExistingSubscription_FiltersBlockedSenderAfterBlock_AndStillDeliversOtherSenders(string kind)
    {
        var data = await SeedAsync();
        await using var subscriber = await BlockingGraphQlClient.CreateAsync(fixture.ConnectionString, data.Actor.UserId);
        await using var writer = await BlockingGraphQlClient.CreateAsync(fixture.ConnectionString, data.Actor.UserId);
        var (document, topic) = kind switch
        {
            "post" => ("subscription { postAdded { id body profileId } }", "post_added"),
            "comment" or "nested-comment" => ("subscription { commentAdded { id body profileId } }", "comment_added"),
            "feed" => ("subscription { feedEventAdded { id body profileId } }", "feed_event_added"),
            "group-changed" => ($$"""subscription { groupChanged(groupId: "{{data.Group.Id}}") { id lastMessage { body senderProfileId } messages { body senderProfileId } members { profile { id } } } }""", $"group_{data.Group.Id}_changed"),
            _ => ($$"""subscription { groupMessageAdded(groupId: "{{data.Group.Id}}") { id body senderProfileId } }""", $"group_{data.Group.Id}"),
        };
        await using var result = await subscriber.SubscribeAsync(document);
        var stream = Assert.IsAssignableFrom<IResponseStream>(result);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var events = stream.ReadResultsAsync().GetAsyncEnumerator(timeout.Token);
        var pending = events.MoveNextAsync().AsTask();
        var sender = subscriber.GetService<ITopicEventSender>();
        await PublishAsync(kind, topic, sender, data, blocked: true);
        Assert.True(await pending.WaitAsync(timeout.Token));
        Assert.Contains(data.Hidden, events.Current.ToJson());
        pending = events.MoveNextAsync().AsTask();

        await writer.BlockAsync(data.Peer.Id);
        await PublishAsync(kind, topic, sender, data, blocked: true);
        await PublishAsync(kind, topic, sender, data, blocked: false);

        // Se permite omitir el evento o producir un payload nulo; nunca exponer su contenido.
        var sawVisible = false;
        for (var i = 0; i < 2; i++)
        {
            Assert.True(await pending.WaitAsync(timeout.Token));
            var json = events.Current.ToJson();
            Assert.DoesNotContain(data.Hidden, json);
            Assert.DoesNotContain(data.Peer.Id.ToString(), json, StringComparison.OrdinalIgnoreCase);
            if (json.Contains(data.Visible, StringComparison.Ordinal))
            {
                sawVisible = true;
                break;
            }
            pending = events.MoveNextAsync().AsTask();
        }
        Assert.True(sawVisible, "La suscripción debe seguir entregando eventos de otros perfiles.");
    }

    [Fact]
    public async Task ExistingDirectMessageSubscription_DoesNotExposeQueuedMessageAfterBlock()
    {
        var data = await SeedAsync();
        await using var subscriber = await BlockingGraphQlClient.CreateAsync(fixture.ConnectionString, data.Actor.UserId);
        await using var writer = await BlockingGraphQlClient.CreateAsync(fixture.ConnectionString, data.Actor.UserId);
        var key = CommunityTestData.ConversationKey(data.Actor.Id, data.Peer.Id);
        await using var result = await subscriber.SubscribeAsync($$"""
            subscription { messageAdded(conversationKey: "{{key}}") { id body senderProfileId } }
            """);
        var stream = Assert.IsAssignableFrom<IResponseStream>(result);
        using var cancel = new CancellationTokenSource();
        await using var events = stream.ReadResultsAsync().GetAsyncEnumerator(cancel.Token);
        var pending = events.MoveNextAsync().AsTask();
        var sender = subscriber.GetService<ITopicEventSender>();
        await sender.SendAsync($"message_{key}", data.DirectMessage);
        Assert.True(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains(data.Hidden, events.Current.ToJson());
        pending = events.MoveNextAsync().AsTask();
        await writer.BlockAsync(data.Peer.Id);
        await sender.SendAsync($"message_{key}", data.DirectMessage);
        await AssertSuppressedAsync(events, pending, cancel, data.Hidden);
    }

    [Fact]
    public async Task ExistingGroupSubscription_StopsDeliveringAfterMembershipIsRemoved()
    {
        var data = await SeedAsync();
        await using var subscriber = await BlockingGraphQlClient.CreateAsync(fixture.ConnectionString, data.Actor.UserId);
        await using var result = await subscriber.SubscribeAsync($$"""
            subscription { groupMessageAdded(groupId: "{{data.Group.Id}}") { id body } }
            """);
        var stream = Assert.IsAssignableFrom<IResponseStream>(result);
        using var cancel = new CancellationTokenSource();
        await using var events = stream.ReadResultsAsync().GetAsyncEnumerator(cancel.Token);
        var sender = subscriber.GetService<ITopicEventSender>();
        var pending = events.MoveNextAsync().AsTask();
        await sender.SendAsync($"group_{data.Group.Id}", data.VisibleMessage);
        Assert.True(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains(data.Visible, events.Current.ToJson());
        pending = events.MoveNextAsync().AsTask();
        await using (var db = new CommunityTestContext(fixture.ConnectionString).Create())
        {
            await db.ChatGroupMembers.Where(m => m.GroupId == data.Group.Id && m.ProfileId == data.Actor.Id).ExecuteDeleteAsync();
        }
        await sender.SendAsync($"group_{data.Group.Id}", data.VisibleMessage);
        await AssertSuppressedAsync(events, pending, cancel, data.Visible);
    }

    [Theory]
    [InlineData("clubNotificationAdded")]
    [InlineData("clubMembershipChanged")]
    public async Task PersonalClubSubscriptions_RejectAnotherProfilesTopic(string field)
    {
        var data = await SeedAsync();
        await using var subscriber = await BlockingGraphQlClient.CreateAsync(fixture.ConnectionString, data.Actor.UserId);
        await using var result = await subscriber.SubscribeAsync($$"""
            subscription { {{field}}(profileId: "{{data.Peer.Id}}") { profileId } }
            """);
        Assert.False(result is IResponseStream, "El tópico personal debe autorizar al propietario antes de suscribirse.");
        Assert.NotEmpty(result.ExpectOperationResult().Errors!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrivateClubSubscriptions_RequireMembershipInitiallyAndAfterOpening(bool liveChat)
    {
        var data = await SeedAsync();
        var club = new Club
        {
            Id = Guid.NewGuid(), Slug = $"qa-private-stream-{Guid.NewGuid():N}", Name = "synthetic private club",
            Description = "QA", Category = "QA", CreatedByProfileId = data.Actor.Id, Visibility = ClubVisibility.Privado,
        };
        var live = new LiveSession { Id = Guid.NewGuid(), ClubId = club.Id, Title = "synthetic live", ScheduledStartAt = DateTime.UtcNow };
        var post = new Post
        {
            Id = Guid.NewGuid(), ProfileId = data.VisiblePost.ProfileId, ClubId = club.Id, Body = data.Visible,
            ClubStatus = ClubPostStatus.Publicado, ClubVisibility = ClubPostVisibility.Privado,
        };
        var message = new LiveChatMessage { Id = Guid.NewGuid(), LiveSessionId = live.Id, SenderProfileId = data.VisiblePost.ProfileId, Body = data.Visible };
        await using (var db = new CommunityTestContext(fixture.ConnectionString).Create())
        {
            db.Clubs.Add(club);
            db.ClubMembers.Add(new ClubMember { ClubId = club.Id, ProfileId = data.Actor.Id });
            db.LiveSessions.Add(live);
            db.Posts.Add(post);
            db.LiveChatMessages.Add(message);
            await db.SaveChangesAsync();
        }
        var document = liveChat
            ? $$"""subscription { liveChatMessageAdded(liveId: "{{live.Id}}") { id body } }"""
            : $$"""subscription { clubPostAdded(clubId: "{{club.Id}}") { id body } }""";
        await using var outsider = await BlockingGraphQlClient.CreateAsync(fixture.ConnectionString, data.Peer.UserId);
        await using (var denied = await outsider.SubscribeAsync(document))
        {
            Assert.False(denied is IResponseStream, "Un no miembro no puede abrir el tópico privado.");
            Assert.NotEmpty(denied.ExpectOperationResult().Errors!);
        }
        await using var subscriber = await BlockingGraphQlClient.CreateAsync(fixture.ConnectionString, data.Actor.UserId);
        await using var result = await subscriber.SubscribeAsync(document);
        var stream = Assert.IsAssignableFrom<IResponseStream>(result);
        using var cancel = new CancellationTokenSource();
        await using var events = stream.ReadResultsAsync().GetAsyncEnumerator(cancel.Token);
        var sender = subscriber.GetService<ITopicEventSender>();
        var pending = events.MoveNextAsync().AsTask();
        async ValueTask PublishAsync()
        {
            if (liveChat) await sender.SendAsync($"live_{live.Id}_chat", message);
            else await sender.SendAsync($"club_{club.Id}_posts", post);
        }
        await PublishAsync();
        Assert.True(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains(data.Visible, events.Current.ToJson());
        pending = events.MoveNextAsync().AsTask();
        await using (var db = new CommunityTestContext(fixture.ConnectionString).Create())
        {
            await db.ClubMembers.Where(m => m.ClubId == club.Id && m.ProfileId == data.Actor.Id).ExecuteDeleteAsync();
        }
        await PublishAsync();
        await AssertSuppressedAsync(events, pending, cancel, data.Visible);
    }

    private static async Task AssertSuppressedAsync(
        IAsyncEnumerator<OperationResult> events, Task<bool> pending, CancellationTokenSource cancel, string hidden)
    {
        try
        {
            if (await pending.WaitAsync(TimeSpan.FromSeconds(2)))
            {
                var json = events.Current.ToJson();
                Assert.DoesNotContain(hidden, json);
            }
        }
        catch (TimeoutException)
        {
            // Un filtro que omite el evento no produce un frame; cancelar libera el lector.
            cancel.Cancel();
            try { await pending; } catch (OperationCanceledException) { }
        }
    }

    private async Task<SubscriptionData> SeedAsync()
    {
        if (!fixture.Available)
            throw new InvalidOperationException("COP_TEST_DB_CONNECTION local is required for blocking regressions.");
        await using var db = new CommunityTestContext(fixture.ConnectionString).Create();
        var actor = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "synthetic subscriber");
        var peer = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "synthetic blocked sender");
        var neutral = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), "synthetic visible sender");
        await CommunityTestData.SeedFollowAsync(db, actor.Id, peer.Id);
        await CommunityTestData.SeedFollowAsync(db, peer.Id, actor.Id);
        var hidden = $"hidden-event-{Guid.NewGuid():N}";
        var visible = $"visible-event-{Guid.NewGuid():N}";
        var blockedPost = new Post { Id = Guid.NewGuid(), ProfileId = peer.Id, Body = hidden };
        var visiblePost = new Post { Id = Guid.NewGuid(), ProfileId = neutral.Id, Body = visible };
        var blockedComment = new Comment { Id = Guid.NewGuid(), PostId = visiblePost.Id, ProfileId = peer.Id, Body = hidden };
        var visibleComment = new Comment { Id = Guid.NewGuid(), PostId = visiblePost.Id, ProfileId = neutral.Id, Body = visible };
        var nestedComment = new Comment { Id = Guid.NewGuid(), PostId = visiblePost.Id, ProfileId = neutral.Id, Body = hidden, ParentCommentId = blockedComment.Id };
        var blockedFeed = new FeedEvent { Id = Guid.NewGuid(), ProfileId = peer.Id, Body = hidden, Kind = FeedEventKind.Publicacion };
        var visibleFeed = new FeedEvent { Id = Guid.NewGuid(), ProfileId = neutral.Id, Body = visible, Kind = FeedEventKind.Publicacion };
        var group = new ChatGroup { Id = Guid.NewGuid(), Name = "synthetic subscription group", CreatedByProfileId = actor.Id };
        var blockedMessage = new Message { Id = Guid.NewGuid(), SenderProfileId = peer.Id, ConversationId = group.Id, Body = hidden, CreatedAt = DateTime.UtcNow.AddMinutes(1) };
        var visibleMessage = new Message { Id = Guid.NewGuid(), SenderProfileId = neutral.Id, ConversationId = group.Id, Body = visible, CreatedAt = DateTime.UtcNow };
        var directMessage = new Message { Id = Guid.NewGuid(), SenderProfileId = peer.Id, RecipientProfileId = actor.Id, Body = hidden, CreatedAt = DateTime.UtcNow };
        db.Posts.AddRange(blockedPost, visiblePost);
        db.Comments.AddRange(blockedComment, visibleComment, nestedComment);
        db.FeedEvents.AddRange(blockedFeed, visibleFeed);
        db.ChatGroups.Add(group);
        db.ChatGroupMembers.AddRange(new[] { actor, peer, neutral }.Select(p => new ChatGroupMember { GroupId = group.Id, ProfileId = p.Id }));
        db.Messages.AddRange(blockedMessage, visibleMessage, directMessage);
        await db.SaveChangesAsync();
        return new SubscriptionData(actor, peer, group, hidden, visible, blockedPost, visiblePost,
            blockedComment, visibleComment, nestedComment, blockedFeed, visibleFeed, blockedMessage, visibleMessage, directMessage);
    }

    private static ValueTask PublishAsync(string kind, string topic, ITopicEventSender sender, SubscriptionData data, bool blocked)
        => kind switch
        {
            "post" => sender.SendAsync(topic, blocked ? data.BlockedPost : data.VisiblePost),
            "comment" => sender.SendAsync(topic, blocked ? data.BlockedComment : data.VisibleComment),
            "nested-comment" => sender.SendAsync(topic, blocked ? data.NestedComment : data.VisibleComment),
            "feed" => sender.SendAsync(topic, blocked ? data.BlockedFeed : data.VisibleFeed),
            "group-changed" => sender.SendAsync(topic, data.Group),
            _ => sender.SendAsync(topic, blocked ? data.BlockedMessage : data.VisibleMessage),
        };

    private sealed record SubscriptionData(Profile Actor, Profile Peer, ChatGroup Group, string Hidden, string Visible,
        Post BlockedPost, Post VisiblePost, Comment BlockedComment, Comment VisibleComment, Comment NestedComment,
        FeedEvent BlockedFeed, FeedEvent VisibleFeed, Message BlockedMessage, Message VisibleMessage, Message DirectMessage);
}
