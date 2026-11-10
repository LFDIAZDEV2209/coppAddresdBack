using System.Security.Claims;
using System.Text.Json;
using CoppAddresd.Application.DTOs.Storage;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Community.GraphQL.Mutations;
using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.GraphQL.Resolvers;
using CoppAddresd.Community.GraphQL.Subscriptions;
using CoppAddresd.Community.Persistence;
using CoppAddresd.Community.Security;
using CoppAddresd.Community.Storage;
using HotChocolate;
using HotChocolate.Execution;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoppAddresd.Community.IntegrationTests;

/// <summary>
/// Ejecuta el contrato GraphQL sobre PostgreSQL aislado. Solo sustituye la identidad
/// autenticada y el storage; no inicia hosts, proveedores externos ni procesos de fondo.
/// Cada operación tiene un scope/DbContext nuevo, como una petición HTTP real.
/// </summary>
internal sealed class BlockingGraphQlClient : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly ClaimsPrincipal _user;
    private readonly IRequestExecutor _executor;

    private BlockingGraphQlClient(ServiceProvider services, ClaimsPrincipal user, IRequestExecutor executor)
        => (_services, _user, _executor) = (services, user, executor);

    public static async Task<BlockingGraphQlClient> CreateAsync(
        string connectionString, Guid? userId, bool moderator = false, string audience = "app")
    {
        var claims = userId is null ? [] : new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.Value.ToString()),
            new("permission", "Community.View"),
            new("aud", audience),
        };
        if (moderator) claims.Add(new Claim("permission", "Community.Moderate"));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, userId is null ? null : "test"));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHttpContextAccessor>(new FixedHttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = user },
        });
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(new StorageSignatureService("blocking-tests-only-ephemeral-key"));
        services.AddSingleton<IObjectStorageService, NoExternalStorage>();
        services.AddDbContext<CommunityDbContext>(options => options.UseNpgsql(connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "community")));
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Community.View", policy => policy.RequireClaim("permission", "Community.View"));
            options.AddPolicy("CommunityModerator", policy => policy.RequireClaim("permission", "Community.Moderate"));
        });
        services.AddGraphQLServer()
            .AddQueryType<CommunityQuery>()
            .AddMutationType<CommunityMutation>()
            .AddSubscriptionType<CommunitySubscription>()
            .AddTypeExtension<ClubQuery>()
            .AddTypeExtension<ClubMutation>()
            .AddTypeExtension<ClubSubscription>()
            .AddType<PostImageUrlResolver>()
            .AddType<ProfileImageUrlResolver>()
            .AddTypeExtension<ProfileResolvers>()
            .AddTypeExtension<PollVoteResolvers>()
            .AddTypeExtension<ClubResolvers>()
            .AddTypeExtension<ClubEventResolvers>()
            .AddTypeExtension<ClubMediaResolvers>()
            .AddAuthorization()
            .AddInMemorySubscriptions();
        var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();
        return new BlockingGraphQlClient(provider, user, executor);
    }

    public async Task<JsonElement> ExecuteAsync(string document, bool expectErrors = false)
    {
        await using var scope = _services.CreateAsyncScope();
        await InitializeRequestAsync(scope.ServiceProvider);
        await using var result = await _executor.ExecuteAsync(Request(document, scope.ServiceProvider));
        using var json = JsonDocument.Parse(result.ToJson());
        var root = json.RootElement.Clone();
        if (expectErrors)
            Assert.True(root.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0, root.ToString());
        else
            Assert.False(root.TryGetProperty("errors", out _), root.ToString());
        return root;
    }

    public async Task BlockAsync(Guid profileId)
    {
        var result = await ExecuteAsync($$"""mutation { blockUser(profileId: "{{profileId}}") }""");
        Assert.True(result.GetProperty("data").GetProperty("blockUser").GetBoolean());
    }

    public async Task UnblockAsync(Guid profileId)
    {
        var result = await ExecuteAsync($$"""mutation { unblockUser(profileId: "{{profileId}}") }""");
        Assert.True(result.GetProperty("data").GetProperty("unblockUser").GetBoolean());
    }

    public async Task<IExecutionResult> SubscribeAsync(string document)
    {
        await InitializeRequestAsync(_services);
        return await _executor.ExecuteAsync(Request(document, _services));
    }

    public T GetService<T>() where T : notnull => _services.GetRequiredService<T>();

    private IOperationRequest Request(string document, IServiceProvider services)
        => OperationRequestBuilder.New().SetDocument(document).SetServices(services).SetUser(_user).Build();

    private static Task InitializeRequestAsync(IServiceProvider services)
        => CommunityBlockPolicy.InitializeRequestAsync(services.GetRequiredService<CommunityDbContext>(),
            services.GetRequiredService<IHttpContextAccessor>(), CancellationToken.None);

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    // El accessor real usa AsyncLocal estático; este doble mantiene independientes
    // las identidades de clientes concurrentes y de sus scopes de ejecución.
    private sealed class FixedHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    /// <summary>Cualquier acceso de medios externo falla de inmediato durante estas pruebas.</summary>
    private sealed class NoExternalStorage : IObjectStorageService
    {
        public bool IsCloudStorage => false;
        public Task<string> PutObjectAsync(string key, Stream content, string? contentType = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Stream> GetObjectAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ObjectMetadata?> HeadObjectAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ListObjectsResult> ListObjectsAsync(string prefix, string? continuationToken = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteObjectAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteObjectsAsync(IEnumerable<string> keys, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CopyObjectAsync(string sourceKey, string destinationKey, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> GetPreSignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> GetPreSignedUploadUrlAsync(string key, string? contentType, TimeSpan expiry, string publicBaseUrl, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
