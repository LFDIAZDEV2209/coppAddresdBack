using CoppAddresd.Community.GraphQL.Mutations;
using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.GraphQL.Resolvers;
using CoppAddresd.Community.GraphQL.Subscriptions;
using HotChocolate.Execution;
using HotChocolate.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CoppAddresd.Community.UnitTests;

public sealed class ProfileBlockingSchemaTests
{
    [Fact]
    public async Task BlockingSchema_ExposesOnlyTargetArgumentAndSafeBlockedProfileSummary()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        var schema = await services.AddGraphQLServer()
            .AddAuthorization()
            .AddQueryType<CommunityQuery>()
            .AddMutationType<CommunityMutation>()
            .AddSubscriptionType<CommunitySubscription>()
            .AddTypeExtension<ProfileResolvers>()
            .BuildSchemaAsync();

        foreach (var name in new[] { "blockUser", "unblockUser" })
        {
            var field = schema.MutationType!.Fields[name];
            Assert.Equal("Boolean!", field.Type.ToString());
            var argument = Assert.Single(field.Arguments);
            Assert.Equal("profileId", argument.Name);
            Assert.Equal("UUID!", argument.Type.ToString());
        }

        var blockedProfiles = schema.QueryType.Fields["blockedProfiles"];
        Assert.Equal("[BlockedProfile!]!", blockedProfiles.Type.ToString());
        Assert.Equal(new[] { "skip", "take" }, blockedProfiles.Arguments.Select(a => a.Name).Order());
        Assert.Equal("50", blockedProfiles.Arguments["take"].DefaultValue?.ToString());
        Assert.Equal("0", blockedProfiles.Arguments["skip"].DefaultValue?.ToString());
        var summary = schema.Types.GetType<ObjectType>("BlockedProfile");
        Assert.Equal(new[] { "avatarUrl", "createdAt", "displayName", "profileId" },
            summary.Fields.Where(f => !f.Name.StartsWith("__", StringComparison.Ordinal)).Select(f => f.Name).Order());
    }
}
