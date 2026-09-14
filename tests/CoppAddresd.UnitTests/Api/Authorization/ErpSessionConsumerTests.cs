using System.Net;
using System.Security.Claims;
using CoppAddresd.Shared.Security;

namespace CoppAddresd.UnitTests.Api.Authorization;

public sealed class ErpSessionConsumerTests
{
    [Theory]
    [InlineData(HttpStatusCode.NoContent, true)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    public async Task ValidateAsync_Erp_UsaAutoridadYPropagaBearer(
        HttpStatusCode status,
        bool expected
    )
    {
        using var handler = new ResponseHandler(status);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://auth.test") };
        Assert.Equal(
            expected,
            await new ErpSessionValidation(http).ValidateAsync(
                Principal("erp"),
                "erp-token",
                CancellationToken.None
            )
        );
        Assert.Equal(1, handler.Calls);
        Assert.Equal("Bearer erp-token", handler.Authorization);
        Assert.Equal("/api/auth/session/validate", handler.Path);
    }

    [Fact]
    public async Task ValidateAsync_AuthNoDisponible_NoAutorizaErp()
    {
        using var handler = new ResponseHandler(HttpStatusCode.ServiceUnavailable);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://auth.test") };
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new ErpSessionValidation(http).ValidateAsync(
                Principal("erp"),
                "erp-token",
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task ValidateAsync_App_NoConsultaAutoridadErp()
    {
        using var handler = new ResponseHandler(HttpStatusCode.ServiceUnavailable);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://auth.test") };
        Assert.True(
            await new ErpSessionValidation(http).ValidateAsync(
                Principal("app"),
                "app-token",
                CancellationToken.None
            )
        );
        Assert.Equal(0, handler.Calls);
    }

    private static ClaimsPrincipal Principal(string audience) =>
        new(new ClaimsIdentity([new Claim("aud", audience)], "test"));

    private sealed class ResponseHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Authorization { get; private set; }
        public string? Path { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            Calls++;
            Authorization = request.Headers.Authorization?.ToString();
            Path = request.RequestUri?.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
