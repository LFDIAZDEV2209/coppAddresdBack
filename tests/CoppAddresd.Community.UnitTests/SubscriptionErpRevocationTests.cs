using System.Collections.Immutable;
using System.Net;
using System.Security.Claims;
using CoppAddresd.Community.GraphQL;
using CoppAddresd.Shared.Security;
using HotChocolate;
using HotChocolate.AspNetCore.Subscriptions;
using HotChocolate.Execution;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace CoppAddresd.Community.UnitTests;

/// <summary>Sesión WebSocket ya autenticada: revocación antes de ejecutar y entregar resultados.</summary>
public sealed class SubscriptionErpRevocationTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task SesionErpExistente_RevocadaONoVerificable_RechazaPeticionYResultado(
        HttpStatusCode status
    )
    {
        using var handler = new AuthorityHandler(status);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://auth.test") };
        using var services = new ServiceCollection()
            .AddSingleton(new ErpSessionValidation(http))
            .BuildServiceProvider();
        var session = Session("erp", services);
        var interceptor = Interceptor();
        await using var result = new OperationResult(
            ImmutableList.Create<IError>(
                ErrorBuilder.New().SetMessage("Resultado sintético").Build()
            ),
            null
        );
        await Assert.ThrowsAsync<GraphQLException>(() =>
            interceptor
                .OnRequestAsync(
                    session,
                    "operation",
                    OperationRequestBuilder.New(),
                    CancellationToken.None
                )
                .AsTask()
        );
        await Assert.ThrowsAsync<GraphQLException>(() =>
            interceptor.OnResultAsync(session, "operation", result, CancellationToken.None).AsTask()
        );
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task SesionAppExistente_ConservaOperacionYResultadoSinConsultarAuth()
    {
        using var handler = new AuthorityHandler(HttpStatusCode.Unauthorized);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://auth.test") };
        using var services = new ServiceCollection()
            .AddSingleton(new ErpSessionValidation(http))
            .BuildServiceProvider();
        var session = Session("app", services);
        var interceptor = Interceptor();
        await using var result = new OperationResult(
            ImmutableList.Create<IError>(
                ErrorBuilder.New().SetMessage("Resultado sintético").Build()
            ),
            null
        );
        await interceptor.OnRequestAsync(
            session,
            "operation",
            OperationRequestBuilder.New(),
            CancellationToken.None
        );
        Assert.Same(
            result,
            await interceptor.OnResultAsync(session, "operation", result, CancellationToken.None)
        );
        Assert.Equal(0, handler.Calls);
    }

    private static ISocketSession Session(string audience, IServiceProvider services)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("aud", audience)], "test")),
        };
        context.Items[ErpSessionValidation.TokenKey] = "existing-session-token";
        var session = Substitute.For<ISocketSession>();
        session.Connection.HttpContext.Returns(context);
        return session;
    }

    private static SubscriptionAuthInterceptor Interceptor() =>
        new(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Jwt:Secret"] = new string('x', 64),
                        ["Jwt:Issuer"] = "test",
                    }
                )
                .Build()
        );

    private sealed class AuthorityHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
