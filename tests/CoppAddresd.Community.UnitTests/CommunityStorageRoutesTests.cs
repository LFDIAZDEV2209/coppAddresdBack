using CoppAddresd.Application.Interfaces;
using CoppAddresd.Community.Security;
using CoppAddresd.Community.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace CoppAddresd.Community.UnitTests;

public sealed class CommunityStorageRoutesTests
{
    [Fact]
    public async Task CommunityAlias_MapsAuthenticatedUpload_AndRetainsLegacyRoute()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IObjectStorageService>());
        builder.Services.AddSingleton(new StorageSignatureService("qa-storage-signature-key"));
        await using var app = builder.Build();
        app.MapPostStorageEndpoints();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
        var upload = Assert.Single(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == "/api/v1/community/storage/{**key}"
            && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains("PUT"));
        Assert.NotNull(upload.Metadata.GetMetadata<IAuthorizeData>());
        Assert.Contains(endpoints, endpoint => endpoint.RoutePattern.RawText == "/storage/{**key}");
    }
}
