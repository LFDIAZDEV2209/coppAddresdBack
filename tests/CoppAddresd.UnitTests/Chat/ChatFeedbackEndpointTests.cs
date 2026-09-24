using System.Security.Claims;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Chat;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Chat;

/// <summary>
/// Endpoint <c>POST /api/v1/chat/feedback</c> (Fase 9): delega a MediatR con el
/// <c>userId</c> del JWT, exige autenticación y traduce los fallos del
/// ai-service a <c>ProblemDetails</c> 502/503 con mensaje clínico amigable.
/// </summary>
public sealed class ChatFeedbackEndpointTests
{
    private const string FriendlyDetail =
        "El asistente inteligente no está disponible temporalmente. Intente en unos minutos.";

    private static ChatController BuildController(IMediator mediator, string? jwtUserId)
    {
        return new ChatController(mediator, NullLogger<ChatController>.Instance)
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
    }

    [Fact]
    public async Task SendFeedback_DelegaAMediatRConUserIdDelJwt()
    {
        // Arrange.
        const string jwtUser = "paciente-123";
        var mediator = Substitute.For<IMediator>();
        SendChatFeedbackCommand? captured = null;
        mediator
            .Send(Arg.Any<SendChatFeedbackCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<SendChatFeedbackCommand>();
                return new ChatFeedbackResponseDto("t1", 5, true, "success");
            });
        var controller = BuildController(mediator, jwtUser);

        // Act.
        var result = await controller.SendFeedback(
            new ChatFeedbackRequestDto("e1", "t1", 5, "Muy útil"),
            CancellationToken.None
        );

        // Assert: 200 + comando con identidad del JWT (el body no trae user).
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ChatFeedbackResponseDto>(ok.Value);
        Assert.Equal("t1", body.ThreadId);
        Assert.NotNull(captured);
        Assert.Equal("e1", captured!.ExecutionId);
        Assert.Equal("t1", captured.ThreadId);
        Assert.Equal(5, captured.Rating);
        Assert.Equal("Muy útil", captured.Comment);
        Assert.Equal(jwtUser, captured.UserId);
    }

    [Fact]
    public async Task SendFeedback_SinJwt_RetornaUnauthorizedSinLlamarAMediatR()
    {
        var mediator = Substitute.For<IMediator>();
        var controller = BuildController(mediator, jwtUserId: null);

        var result = await controller.SendFeedback(
            new ChatFeedbackRequestDto("e1", "t1", 4, null),
            CancellationToken.None
        );

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        await mediator
            .DidNotReceiveWithAnyArgs()
            .Send(Arg.Any<SendChatFeedbackCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendFeedback_AiServiceRespondeError_Retorna502ConMensajeClinico()
    {
        var mediator = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<SendChatFeedbackCommand>(), Arg.Any<CancellationToken>())
            .Returns<Task<ChatFeedbackResponseDto>>(_ =>
                throw new AiServiceException(500, "boom interno")
            );
        var controller = BuildController(mediator, "paciente-123");

        var result = await controller.SendFeedback(
            new ChatFeedbackRequestDto("e1", "t1", 2, "No ayudó"),
            CancellationToken.None
        );

        var status = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, status.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(status.Value);
        Assert.Equal(StatusCodes.Status502BadGateway, problem.Status);
        Assert.Equal(FriendlyDetail, problem.Detail);
        // Sin fuga de trazas internas.
        Assert.DoesNotContain("boom", problem.Detail);
    }

    [Fact]
    public async Task SendFeedback_AiServiceOffline_Retorna503ConMensajeClinico()
    {
        var mediator = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<SendChatFeedbackCommand>(), Arg.Any<CancellationToken>())
            .Returns<Task<ChatFeedbackResponseDto>>(_ =>
                throw new HttpRequestException("connection refused")
            );
        var controller = BuildController(mediator, "paciente-123");

        var result = await controller.SendFeedback(
            new ChatFeedbackRequestDto("e1", "t1", 5, null),
            CancellationToken.None
        );

        var status = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(status.Value);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.Status);
        Assert.Equal(FriendlyDetail, problem.Detail);
        Assert.DoesNotContain("connection refused", problem.Detail);
    }
}
