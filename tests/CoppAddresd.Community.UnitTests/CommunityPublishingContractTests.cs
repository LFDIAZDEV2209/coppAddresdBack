using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Mutations;
using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.Storage;
using HotChocolate;

namespace CoppAddresd.Community.UnitTests;

public sealed class CommunityPublishingContractTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(101, 0)]
    [InlineData(10, -1)]
    public async Task DirectoryRejectsInvalidPaginationBeforeDatabaseAccess(int take, int skip)
        => await Assert.ThrowsAsync<GraphQLException>(() => new CommunityQuery().ProfilesPage(
            null!, null!, CancellationToken.None, take: take, skip: skip));

    [Theory]
    [InlineData("¿QA?", "A", "a")]
    [InlineData("¿QA?", "A", "")]
    [InlineData("Q", "A", "B")]
    public async Task AnnouncementRejectsInvalidPollBeforeDatabaseAccess(string question, string a, string b)
        => await Assert.ThrowsAsync<GraphQLException>(() => new CommunityMutation().CreateAnnouncement(
            question, PostType.Encuesta, null, null!, null!, CancellationToken.None, pollOptions: [a, b]));

    [Theory]
    [InlineData(null)]
    [InlineData("http://example.com/image.jpg")]
    [InlineData("javascript:alert(1)")]
    public async Task AnnouncementRejectsMissingOrInvalidAttachment(string? url)
        => await Assert.ThrowsAsync<GraphQLException>(() => new CommunityMutation().CreateAnnouncement(
            "QA", PostType.Imagen, null, null!, null!, CancellationToken.None, mediaUrl: url));

    [Theory]
    [InlineData(PostType.Imagen, "IMAGE")]
    [InlineData(PostType.Video, "VIDEO")]
    public async Task ExternalMediaResolvesWithoutStorageAccess(PostType type, string expected)
    {
        var post = new Post { ImageKey = "https://example.com/qa?format=media", Type = type };
        var resolver = new PostImageUrlResolver();
        Assert.Equal(post.ImageKey, await resolver.GetImageUrlAsync(post, null!, null!, null!, CancellationToken.None));
        Assert.Equal(expected, resolver.GetMediaType(post));
    }
}
