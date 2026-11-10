using System.Text.Json;
using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.IntegrationTests;

/// <summary>
/// Regresiones del contrato público de bloqueo: identidad del JWT, elección dirigida,
/// exclusión simétrica y conservación de evidencia. Todos los datos son sintéticos.
/// </summary>
[Collection(CommunityTestCollection.Name)]
public sealed class ProfileBlockingTests(CommunityTestDatabase fixture)
{
    private CommunityTestContext Factory => fixture.Available
        ? new CommunityTestContext(fixture.ConnectionString)
        : throw new InvalidOperationException("COP_TEST_DB_CONNECTION local is required for blocking regressions.");

    [Fact]
    public async Task BlockUser_IsOwnedByActor_IdempotentAndRemovesBothFollows_WithoutDeletingHistory()
    {
        var (me, peer, _) = await SeedProfilesAsync();
        await using (var db = Factory.Create())
        {
            await CommunityTestData.SeedFollowAsync(db, me.Id, peer.Id);
            await CommunityTestData.SeedFollowAsync(db, peer.Id, me.Id);
            await CommunityTestData.SeedMessageAsync(db, peer.Id, me.Id, "historical synthetic message", DateTime.UtcNow);
        }
        await using var actor = await ClientAsync(me);
        await using var other = await ClientAsync(peer);

        Assert.Equal(me.Id, Data(await actor.ExecuteAsync("{ me { id } }"), "me").GetProperty("id").GetGuid());
        Assert.Equal(peer.Id, Data(await other.ExecuteAsync("{ me { id } }"), "me").GetProperty("id").GetGuid());

        await actor.BlockAsync(peer.Id);
        await actor.BlockAsync(peer.Id);

        var blocked = Data(await actor.ExecuteAsync("{ blockedProfiles { profileId displayName avatarUrl createdAt } }"), "blockedProfiles");
        var item = Assert.Single(blocked.EnumerateArray());
        Assert.Equal(peer.Id, item.GetProperty("profileId").GetGuid());
        Assert.Equal(peer.DisplayName, item.GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("avatarUrl").ValueKind);
        Assert.True(item.GetProperty("createdAt").GetDateTime() <= DateTime.UtcNow.AddSeconds(1));
        Assert.Empty(Data(await other.ExecuteAsync("{ blockedProfiles { profileId } }"), "blockedProfiles").EnumerateArray());
        await using (var db = Factory.Create())
        {
            Assert.False(await db.Follows.AnyAsync(f =>
                (f.FollowerProfileId == me.Id && f.FollowingProfileId == peer.Id) ||
                (f.FollowerProfileId == peer.Id && f.FollowingProfileId == me.Id)));
            Assert.True(await db.Messages.AnyAsync(m => m.SenderProfileId == peer.Id && m.RecipientProfileId == me.Id));
        }

        await actor.UnblockAsync(peer.Id);
        await actor.UnblockAsync(peer.Id);
        Assert.Empty(Data(await actor.ExecuteAsync("{ blockedProfiles { profileId } }"), "blockedProfiles").EnumerateArray());
        await using var after = Factory.Create();
        Assert.False(await after.Follows.AnyAsync(f =>
            (f.FollowerProfileId == me.Id && f.FollowingProfileId == peer.Id) ||
            (f.FollowerProfileId == peer.Id && f.FollowingProfileId == me.Id)));
    }

    [Fact]
    public async Task UnblockUser_RemovesOnlyActorsChoice_AndReverseBlockStillHidesProfile()
    {
        var (me, peer, _) = await SeedProfilesAsync();
        await using var actor = await ClientAsync(me);
        await using var other = await ClientAsync(peer);
        await actor.BlockAsync(peer.Id);
        await other.BlockAsync(me.Id);

        await actor.UnblockAsync(peer.Id);

        Assert.Empty(Data(await actor.ExecuteAsync("{ blockedProfiles { profileId } }"), "blockedProfiles").EnumerateArray());
        Assert.Equal(me.Id, Assert.Single(Data(await other.ExecuteAsync("{ blockedProfiles { profileId } }"), "blockedProfiles")
            .EnumerateArray()).GetProperty("profileId").GetGuid());
        Assert.Equal(JsonValueKind.Null, Data(await actor.ExecuteAsync($$"""{ profile(id: "{{peer.Id}}") { id } }"""), "profile").ValueKind);
        await other.UnblockAsync(me.Id);
        Assert.Equal(peer.Id, Data(await actor.ExecuteAsync($$"""{ profile(id: "{{peer.Id}}") { id } }"""), "profile").GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlockUser_TwoConcurrentRequests_PreserveDirectedChoicesAndRemoveFollows(bool reverseDirection)
    {
        var (me, peer, _) = await SeedProfilesAsync();
        await using (var db = Factory.Create())
        {
            await CommunityTestData.SeedFollowAsync(db, me.Id, peer.Id);
            await CommunityTestData.SeedFollowAsync(db, peer.Id, me.Id);
        }
        await using var first = await ClientAsync(me);
        await using var second = await ClientAsync(reverseDirection ? peer : me);

        await Task.WhenAll(first.BlockAsync(peer.Id), second.BlockAsync(reverseDirection ? me.Id : peer.Id));

        var ownChoices = Data(await first.ExecuteAsync("{ blockedProfiles { profileId } }"), "blockedProfiles");
        Assert.Equal(peer.Id, Assert.Single(ownChoices.EnumerateArray()).GetProperty("profileId").GetGuid());
        var secondChoices = Data(await second.ExecuteAsync("{ blockedProfiles { profileId } }"), "blockedProfiles");
        Assert.Equal(reverseDirection ? me.Id : peer.Id,
            Assert.Single(secondChoices.EnumerateArray()).GetProperty("profileId").GetGuid());
        await using var check = Factory.Create();
        Assert.False(await check.Follows.AnyAsync(f =>
            (f.FollowerProfileId == me.Id && f.FollowingProfileId == peer.Id) ||
            (f.FollowerProfileId == peer.Id && f.FollowingProfileId == me.Id)));
    }

    [Theory]
    [InlineData("self")]
    [InlineData("system")]
    [InlineData("unknown")]
    public async Task BlockUser_RejectsInvalidTargets_WithoutChangingOwnList(string targetKind)
    {
        var (me, _, _) = await SeedProfilesAsync();
        var target = Guid.NewGuid();
        if (targetKind == "self") target = me.Id;
        if (targetKind == "system")
        {
            await using var db = Factory.Create();
            db.Profiles.Add(new Profile { Id = target, IsSystem = true, DisplayName = "synthetic system", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        await using var actor = await ClientAsync(me);
        await actor.ExecuteAsync($$"""mutation { blockUser(profileId: "{{target}}") }""", expectErrors: true);
        await actor.ExecuteAsync($$"""mutation { unblockUser(profileId: "{{target}}") }""", expectErrors: true);
        Assert.Empty(Data(await actor.ExecuteAsync("{ blockedProfiles { profileId } }"), "blockedProfiles").EnumerateArray());
    }

    [Fact]
    public async Task BlockUser_RequiresAuthenticatedExistingActor_AndCannotAcceptSpoofedActorId()
    {
        var (me, peer, _) = await SeedProfilesAsync();
        await using var anonymous = await BlockingGraphQlClient.CreateAsync(fixture.ConnectionString, null);
        await using var missingProfile = await BlockingGraphQlClient.CreateAsync(fixture.ConnectionString, Guid.NewGuid());
        var mutation = $$"""mutation { blockUser(profileId: "{{peer.Id}}") }""";
        await anonymous.ExecuteAsync(mutation, expectErrors: true);
        await anonymous.ExecuteAsync("{ blockedProfiles { profileId } }", expectErrors: true);
        await missingProfile.ExecuteAsync(mutation, expectErrors: true);
        await using var actor = await ClientAsync(me);
        await actor.ExecuteAsync($$"""mutation { blockUser(profileId: "{{peer.Id}}", actorProfileId: "{{peer.Id}}") }""", expectErrors: true);
        Assert.Empty(Data(await actor.ExecuteAsync("{ blockedProfiles { profileId } }"), "blockedProfiles").EnumerateArray());
    }

    [Fact]
    public async Task BlockedProfiles_PaginatesOnlyOwnChoices_WithStableOrder()
    {
        var (me, peer, third) = await SeedProfilesAsync();
        await using var actor = await ClientAsync(me);
        await actor.BlockAsync(peer.Id);
        await actor.BlockAsync(third.Id);
        var all = Data(await actor.ExecuteAsync("{ blockedProfiles { profileId } }"), "blockedProfiles")
            .EnumerateArray().Select(p => p.GetProperty("profileId").GetGuid()).ToArray();
        var first = Data(await actor.ExecuteAsync("{ blockedProfiles(take: 1, skip: 0) { profileId } }"), "blockedProfiles");
        var second = Data(await actor.ExecuteAsync("{ blockedProfiles(take: 1, skip: 1) { profileId } }"), "blockedProfiles");
        Assert.Equal(2, all.Length);
        Assert.Equal(all[0], Assert.Single(first.EnumerateArray()).GetProperty("profileId").GetGuid());
        Assert.Equal(all[1], Assert.Single(second.EnumerateArray()).GetProperty("profileId").GetGuid());
        Assert.Empty(Data(await actor.ExecuteAsync("{ blockedProfiles(take: 1, skip: 2) { profileId } }"), "blockedProfiles").EnumerateArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Block_HidesFeedSinglePostNestedCommentsRepliesAndReposts_InEitherDirection(bool peerBlocks)
    {
        var (me, peer, third) = await SeedProfilesAsync();
        var hidden = $"hidden-{Guid.NewGuid():N}";
        var visible = $"visible-{Guid.NewGuid():N}";
        var peerPost = NewPost(peer.Id, hidden);
        var thirdPost = NewPost(third.Id, visible);
        var parent = NewComment(thirdPost.Id, third.Id, visible);
        var blockedRoot = NewComment(thirdPost.Id, peer.Id, hidden);
        var neutralReplyToBlockedRoot = NewComment(thirdPost.Id, third.Id, hidden + "-neutral-reply", blockedRoot.Id);
        await using (var db = Factory.Create())
        {
            db.Posts.AddRange(peerPost, thirdPost);
            db.Comments.AddRange(parent, blockedRoot, neutralReplyToBlockedRoot,
                NewComment(thirdPost.Id, third.Id, hidden + "-neutral-grandchild", neutralReplyToBlockedRoot.Id),
                NewComment(thirdPost.Id, peer.Id, hidden, parent.Id));
            db.Reposts.AddRange(
                new Repost { Id = Guid.NewGuid(), ProfileId = me.Id, PostId = peerPost.Id },
                new Repost { Id = Guid.NewGuid(), ProfileId = third.Id, PostId = peerPost.Id },
                new Repost { Id = Guid.NewGuid(), ProfileId = peer.Id, PostId = thirdPost.Id });
            db.Likes.Add(new Like { Id = Guid.NewGuid(), ProfileId = peer.Id, PostId = thirdPost.Id });
            db.FeedEvents.AddRange(
                new FeedEvent { Id = Guid.NewGuid(), ProfileId = peer.Id, Body = hidden, Kind = FeedEventKind.Publicacion },
                new FeedEvent { Id = Guid.NewGuid(), ProfileId = third.Id, Body = visible, Kind = FeedEventKind.Publicacion });
            await db.SaveChangesAsync();
        }
        await using var actor = await ClientAsync(me);
        await using var other = await ClientAsync(peer);
        var document = $$"""
            { hiddenProfile: profile(id: "{{peer.Id}}") { id }
              hiddenPost: post(id: "{{peerPost.Id}}") { id body }
              post(id: "{{thirdPost.Id}}") {
                id body comments { id body profile { id posts { body } } replies { id body profile { id } replies { body } } }
                reposts { profile { id posts { body } } post { body } }
                likes { profile { id posts { body } } }
              }
              me { reposts { post { id body profile { id } } } }
              profile(id: "{{third.Id}}") { posts { id body comments { body replies { body } } } reposts { post { body } } }
            }
            """;
        Assert.Contains(hidden, (await actor.ExecuteAsync(document)).ToString());
        await (peerBlocks ? other.BlockAsync(me.Id) : actor.BlockAsync(peer.Id));
        var direct = await actor.ExecuteAsync(document);
        Assert.Equal(JsonValueKind.Null, Data(direct, "hiddenProfile").ValueKind);
        Assert.Equal(JsonValueKind.Null, Data(direct, "hiddenPost").ValueKind);
        Assert.Contains(visible, direct.ToString());
        Assert.DoesNotContain(hidden, direct.ToString());
        Assert.DoesNotContain(peer.Id.ToString(), direct.ToString(), StringComparison.OrdinalIgnoreCase);
        var feed = await actor.ExecuteAsync($$"""{ feed(search: "{{hidden}}") { id body } }""");
        Assert.Empty(Data(feed, "feed").EnumerateArray());
        var events = await actor.ExecuteAsync("{ feedEvents(take: 100) { body profile { id } } }");
        Assert.Contains(visible, events.ToString());
        Assert.DoesNotContain(hidden, events.ToString());
    }

    [Fact]
    public async Task Block_HidesPeopleFollowsFriendsAndDirectMessages_ButKeepsUnrelatedPeers()
    {
        var (me, peer, third) = await SeedProfilesAsync();
        await using (var db = Factory.Create())
        {
            foreach (var other in new[] { peer, third })
            {
                await CommunityTestData.SeedFollowAsync(db, me.Id, other.Id);
                await CommunityTestData.SeedFollowAsync(db, other.Id, me.Id);
                await CommunityTestData.SeedMessageAsync(db, other.Id, me.Id, other.DisplayName,
                    other.Id == peer.Id ? DateTime.UtcNow.AddMinutes(1) : DateTime.UtcNow);
            }
            await CommunityTestData.SeedFollowAsync(db, third.Id, peer.Id);
            await CommunityTestData.SeedFollowAsync(db, peer.Id, third.Id);
        }
        await using var actor = await ClientAsync(me);
        await using var otherClient = await ClientAsync(peer);
        await actor.BlockAsync(peer.Id);
        foreach (var (client, blockedId) in new[] { (actor, peer.Id), (otherClient, me.Id) })
        {
            var result = await client.ExecuteAsync($$"""
                { people(search: "{{me.DisplayName.Split(' ')[0]}}", take: 100) { profile { id } }
                  following { id } followers { id } friends { id }
                  profileFollowers(profileId: "{{third.Id}}") { id }
                  profileFollowing(profileId: "{{third.Id}}") { id }
                  conversations { peer { id } lastMessage { body } }
                  conversation(peerId: "{{blockedId}}") { body }
                }
                """);
            Assert.DoesNotContain(blockedId.ToString(), result.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Data(result, "conversation").EnumerateArray());
            await client.ExecuteAsync($$"""mutation { followUser(profileId: "{{blockedId}}") { id } }""", expectErrors: true);
            await client.ExecuteAsync($$"""mutation { sendMessage(recipientProfileId: "{{blockedId}}", body: "blocked send") { id } }""", expectErrors: true);
        }
        var mine = await actor.ExecuteAsync("{ friends { id } conversations { peer { id } } }");
        Assert.Contains(third.Id.ToString(), mine.ToString(), StringComparison.OrdinalIgnoreCase);
        var firstVisibleConversation = Data(await actor.ExecuteAsync("{ conversations(take: 1) { peer { id } } }"), "conversations");
        Assert.Equal(third.Id, Assert.Single(firstVisibleConversation.EnumerateArray()).GetProperty("peer").GetProperty("id").GetGuid());
        var secondVisiblePerson = Data(await actor.ExecuteAsync($$"""
            { people(search: "{{me.DisplayName.Split(' ')[0]}}", take: 1, skip: 1) { profile { id } } }
            """), "people");
        Assert.Equal(third.Id, Assert.Single(secondVisiblePerson.EnumerateArray()).GetProperty("profile").GetProperty("id").GetGuid());
        await using var check = Factory.Create();
        Assert.False(await check.Messages.AnyAsync(m => m.Body == "blocked send"));
        Assert.True(await check.Messages.AnyAsync(m => m.SenderProfileId == peer.Id && m.RecipientProfileId == me.Id));
    }

    [Fact]
    public async Task Block_FiltersSharedGroupHistoryLastMessageAndNestedNavigation_WithoutRemovingMemberships()
    {
        var (me, peer, third) = await SeedProfilesAsync();
        var group = new ChatGroup { Id = Guid.NewGuid(), Name = "synthetic shared group", CreatedByProfileId = me.Id };
        var hidden = $"hidden-group-{Guid.NewGuid():N}";
        var visible = $"visible-group-{Guid.NewGuid():N}";
        await using (var db = Factory.Create())
        {
            db.ChatGroups.Add(group);
            db.ChatGroupMembers.AddRange(new[] { me, peer, third }.Select(p => new ChatGroupMember { GroupId = group.Id, ProfileId = p.Id }));
            db.Messages.AddRange(
                new Message { Id = Guid.NewGuid(), ConversationId = group.Id, SenderProfileId = third.Id, Body = visible, CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
                new Message { Id = Guid.NewGuid(), ConversationId = group.Id, SenderProfileId = peer.Id, Body = hidden, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        await using var actor = await ClientAsync(me);
        await actor.BlockAsync(peer.Id);
        var result = await actor.ExecuteAsync($$"""
            { group(groupId: "{{group.Id}}") { body senderProfileId }
              groups { id lastMessage { body } messages { body } members { profile { id } } }
              groupMembers(groupId: "{{group.Id}}") { id }
            }
            """);
        Assert.Contains(visible, result.ToString());
        Assert.DoesNotContain(hidden, result.ToString());
        Assert.DoesNotContain(peer.Id.ToString(), result.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(visible, Assert.Single(Data(result, "groups").EnumerateArray()).GetProperty("lastMessage").GetProperty("body").GetString());
        await using var check = Factory.Create();
        Assert.Equal(3, await check.ChatGroupMembers.CountAsync(m => m.GroupId == group.Id));
        Assert.Equal(2, await check.Messages.CountAsync(m => m.ConversationId == group.Id));
    }

    [Fact]
    public async Task Block_FiltersSharedClubPostsAndComments_AndPreservesModeratorEvidence()
    {
        var (me, peer, third) = await SeedProfilesAsync();
        var club = new Club { Id = Guid.NewGuid(), Slug = $"qa-block-{Guid.NewGuid():N}", Name = "synthetic club", Description = "QA", Category = "QA", CreatedByProfileId = third.Id };
        var hidden = $"hidden-club-{Guid.NewGuid():N}";
        var visible = $"visible-club-{Guid.NewGuid():N}";
        var peerPost = NewPost(peer.Id, hidden);
        var thirdPost = NewPost(third.Id, visible);
        foreach (var post in new[] { peerPost, thirdPost })
        {
            post.ClubId = club.Id;
            post.ClubStatus = ClubPostStatus.Publicado;
            post.ClubVisibility = ClubPostVisibility.Publico;
        }
        var comment = NewComment(thirdPost.Id, peer.Id, hidden);
        await using (var db = Factory.Create())
        {
            db.Clubs.Add(club);
            db.Posts.AddRange(peerPost, thirdPost);
            db.Comments.Add(comment);
            db.PostReports.Add(new PostReport { Id = Guid.NewGuid(), PostId = peerPost.Id, ReportedByProfileId = me.Id, Reason = "synthetic evidence" });
            db.CommentReports.Add(new CommentReport { Id = Guid.NewGuid(), CommentId = comment.Id, ReportedByProfileId = me.Id, Reason = "synthetic evidence" });
            await db.SaveChangesAsync();
        }
        await using var actor = await ClientAsync(me);
        await actor.BlockAsync(peer.Id);
        var feed = await actor.ExecuteAsync($$"""
            { clubFeed(clubId: "{{club.Id}}") { id body comments { body } }
              publicClubPosts { id body comments { body } }
            }
            """);
        Assert.Contains(visible, feed.ToString());
        Assert.DoesNotContain(hidden, feed.ToString());
        await using var appModerator = await BlockingGraphQlClient.CreateAsync(fixture.ConnectionString, me.UserId, moderator: true);
        var appModeratorFeed = await appModerator.ExecuteAsync($$"""{ clubFeed(clubId: "{{club.Id}}") { body } }""");
        Assert.DoesNotContain(hidden, appModeratorFeed.ToString());
        await using var moderator = await BlockingGraphQlClient.CreateAsync(fixture.ConnectionString, me.UserId,
            moderator: true, audience: "erp");
        var evidence = await moderator.ExecuteAsync("{ reportedPosts(take: 100) { post { id body } reports { reason } } reportedComments(take: 100) { comment { id body } } }");
        Assert.Contains(peerPost.Id.ToString(), evidence.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(comment.Id.ToString(), evidence.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(hidden, evidence.ToString());
        await using var check = Factory.Create();
        Assert.Null((await check.Posts.SingleAsync(p => p.Id == peerPost.Id)).DeletedAt);
        Assert.Null((await check.Comments.SingleAsync(c => c.Id == comment.Id)).DeletedAt);
        Assert.True(await check.PostReports.AnyAsync(r => r.PostId == peerPost.Id));
        Assert.True(await check.CommentReports.AnyAsync(r => r.CommentId == comment.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportAfterBlock_PersistsEvidence_AndReturnsNoHiddenContent(bool peerBlocks)
    {
        var (me, peer, _) = await SeedProfilesAsync();
        var hidden = $"hidden-report-{Guid.NewGuid():N}";
        var post = NewPost(peer.Id, hidden);
        var comment = NewComment(post.Id, peer.Id, hidden);
        await using (var db = Factory.Create())
        {
            db.Posts.Add(post);
            db.Comments.Add(comment);
            await db.SaveChangesAsync();
        }
        await using var actor = await ClientAsync(me);
        await using var other = await ClientAsync(peer);
        await (peerBlocks ? other.BlockAsync(me.Id) : actor.BlockAsync(peer.Id));

        var response = await actor.ExecuteAsync($$"""
            mutation {
              reportPost(postId: "{{post.Id}}", reason: "synthetic post evidence", details: "already seen before block") {
                id postId post { id body profile { id displayName } }
              }
              reportComment(commentId: "{{comment.Id}}", reason: "synthetic comment evidence", details: "already seen before block") {
                id commentId comment { id body post { id body } profile { id displayName } }
              }
            }
            """);
        var postReport = Data(response, "reportPost");
        var commentReport = Data(response, "reportComment");
        Assert.NotEqual(Guid.Empty, postReport.GetProperty("id").GetGuid());
        Assert.NotEqual(Guid.Empty, commentReport.GetProperty("id").GetGuid());
        Assert.Equal(post.Id, postReport.GetProperty("postId").GetGuid());
        Assert.Equal(comment.Id, commentReport.GetProperty("commentId").GetGuid());
        Assert.Equal(JsonValueKind.Null, postReport.GetProperty("post").ValueKind);
        Assert.Equal(JsonValueKind.Null, commentReport.GetProperty("comment").ValueKind);
        Assert.DoesNotContain(hidden, response.ToString());
        Assert.DoesNotContain(peer.DisplayName, response.ToString());
        await using var check = Factory.Create();
        Assert.True(await check.PostReports.AnyAsync(r => r.Id == postReport.GetProperty("id").GetGuid()
            && r.PostId == post.Id && r.ReportedByProfileId == me.Id));
        Assert.True(await check.CommentReports.AnyAsync(r => r.Id == commentReport.GetProperty("id").GetGuid()
            && r.CommentId == comment.Id && r.ReportedByProfileId == me.Id));
        Assert.Equal(hidden, (await check.Posts.SingleAsync(p => p.Id == post.Id)).Body);
        Assert.Equal(hidden, (await check.Comments.SingleAsync(c => c.Id == comment.Id)).Body);
    }

    private async Task<(Profile Me, Profile Peer, Profile Third)> SeedProfilesAsync()
    {
        var factory = Factory;
        await using var db = factory.Create();
        var prefix = $"block-{Guid.NewGuid():N}";
        var me = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), $"{prefix} actor");
        var peer = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), $"{prefix} peer");
        var third = await CommunityTestData.SeedProfileAsync(db, Guid.NewGuid(), $"{prefix} neutral");
        return (me, peer, third);
    }

    private Task<BlockingGraphQlClient> ClientAsync(Profile profile)
        => BlockingGraphQlClient.CreateAsync(fixture.ConnectionString, profile.UserId);

    private static JsonElement Data(JsonElement result, string field) => result.GetProperty("data").GetProperty(field);

    private static Post NewPost(Guid profileId, string body)
        => new() { Id = Guid.NewGuid(), ProfileId = profileId, Body = body };

    private static Comment NewComment(Guid postId, Guid profileId, string body, Guid? parentId = null)
        => new() { Id = Guid.NewGuid(), PostId = postId, ProfileId = profileId, Body = body, ParentCommentId = parentId };
}
