using System.Reflection;
using System.Security.Claims;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.Features.Threads;
using CoppAddresd.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Chat;

/// <summary>
/// Blindaje anti-IDOR del <c>ThreadsController</c> (Fase 9): la identidad del
/// dueño del thread proviene estricta y exclusivamente del JWT; cualquier
/// <c>userId</c> por query string se ignora y sin JWT se responde 401.
/// </summary>
public sealed class ThreadsControllerAntiIdorTests
{
    private static ThreadsController BuildController(IAiServiceClient aiService, string? jwtUserId)
    {
        var controller = new ThreadsController(aiService, NullLogger<ThreadsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = jwtUserId is null
                        ? new ClaimsPrincipal(new ClaimsIdentity())
                        : new ClaimsPrincipal(
                            new ClaimsIdentity(
                                [new Claim(ClaimTypes.NameIdentifier, jwtUserId)],
                                "test"
                            )
                        ),
                },
            },
        };
        return controller;
    }

    [Fact]
    public void Controlador_ExigeAutorizacion()
    {
        var attr = typeof(ThreadsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public async Task GetMessages_IgnoraSpoofingPorQueryParam_UsaSoloJwt()
    {
        // Arrange: JWT real = "paciente-real", atacante intenta leer el
        // historial de "paciente-victima" vía query param.
        const string jwtUser = "paciente-real";
        var aiService = Substitute.For<IAiServiceClient>();
        string? capturedUserId = null;
        aiService
            .GetThreadStateAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(callInfo =>
            {
                capturedUserId = callInfo.ArgAt<string>(1);
                return new ThreadStateResult("t1", 0, null, []);
            });
        var controller = BuildController(aiService, jwtUser);

        // Act: query param malicioso + paginación válida.
        var result = await controller.GetMessages(
            "t1",
            "paciente-victima",
            limit: 10,
            before: null,
            CancellationToken.None
        );

        // Assert: 200 y el backend usó el JWT, nunca el query param.
        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(jwtUser, capturedUserId);
        Assert.NotEqual("paciente-victima", capturedUserId);
    }

    [Fact]
    public async Task GetMessages_SinJwt_RetornaUnauthorizedSinLlamarAlAiService()
    {
        var aiService = Substitute.For<IAiServiceClient>();
        var controller = BuildController(aiService, jwtUserId: null);

        var result = await controller.GetMessages(
            "t1",
            "paciente-victima",
            null,
            null,
            CancellationToken.None
        );

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        await aiService
            .DidNotReceiveWithAnyArgs()
            .GetThreadStateAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task GetMessages_ConJwtYQueryNulo_UsaJwt()
    {
        const string jwtUser = "paciente-real";
        var aiService = Substitute.For<IAiServiceClient>();
        string? capturedUserId = null;
        aiService
            .GetThreadStateAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(callInfo =>
            {
                capturedUserId = callInfo.ArgAt<string>(1);
                return new ThreadStateResult("t1", 1, "hola", []);
            });
        var controller = BuildController(aiService, jwtUser);

        var result = await controller.GetMessages(
            "t1",
            userId: null,
            null,
            null,
            CancellationToken.None
        );

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(jwtUser, capturedUserId);
    }
}
