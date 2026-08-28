using CoppAddresd.Community.GraphQL.Mutations;
using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.GraphQL.Resolvers;
using CoppAddresd.Community.GraphQL.Subscriptions;
using HotChocolate;
using HotChocolate.Execution;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CoppAddresd.Community.UnitTests;

/// <summary>
/// Valida que el esquema GraphQL de la comunidad se componga correctamente (tipos,
/// enumerados y resolvers derivados) sin necesidad de base de datos.
/// </summary>
public sealed class CommunitySchemaTests
{
    [Fact]
    public async Task Schema_Builds_WithAllErpTypes()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        var builder = services.AddGraphQLServer()
            .AddAuthorization()
            .AddQueryType<CommunityQuery>()
            .AddMutationType<CommunityMutation>()
            .AddSubscriptionType<CommunitySubscription>()
            .AddTypeExtension<ProfileResolvers>();

        var schema = await builder.BuildSchemaAsync();
        var sdl = schema.ToString();

        Assert.NotNull(schema);
        // Enumerados expuestos al frontend.
        Assert.Contains("enum ProfileRegion", sdl);
        Assert.Contains("enum ProfileDiagnosis", sdl);
        Assert.Contains("enum PostType", sdl);
        Assert.Contains("enum PostDestination", sdl);
        Assert.Contains("enum FeedEventKind", sdl);
        Assert.Contains("enum RiskLevel", sdl);
        // Campos nuevos del perfil.
        Assert.Contains("xpTotal", sdl);
        Assert.Contains("levelName", sdl);
        Assert.Contains("currentStreak", sdl);
        Assert.Contains("bestStreak", sdl);
        Assert.Contains("riskLevel", sdl);
        Assert.Contains("region", sdl);
    }
}
