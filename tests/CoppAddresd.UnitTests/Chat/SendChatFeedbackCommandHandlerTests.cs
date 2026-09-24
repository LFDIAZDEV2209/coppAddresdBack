using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Chat;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Chat;

/// <summary>
/// Handler <c>SendChatFeedbackCommandHandler</c> (Fase 9): invoca al cliente IA
/// con el DTO correcto, propaga el <c>CancellationToken</c> y valida el comando
/// (rating 1-5, ids requeridos, comentario acotado, usuario del JWT).
/// </summary>
public sealed class SendChatFeedbackCommandHandlerTests
{
    private static SendChatFeedbackCommandHandler BuildHandler(IAiServiceClient client) =>
        new(client, NullLogger<SendChatFeedbackCommandHandler>.Instance);

    [Fact]
    public async Task Handle_InvocaAlClienteConDtoYUserIdYPropagaCt()
    {
        // Arrange.
        var client = Substitute.For<IAiServiceClient>();
        ChatFeedbackRequestDto? capturedDto = null;
        string? capturedUserId = null;
        CancellationToken capturedCt = default;
        var expected = new ChatFeedbackResponseDto("t1", 5, true, "success");
        client
            .SendFeedbackAsync(
                Arg.Any<ChatFeedbackRequestDto>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(callInfo =>
            {
                capturedDto = callInfo.Arg<ChatFeedbackRequestDto>();
                capturedUserId = callInfo.Arg<string>();
                capturedCt = callInfo.Arg<CancellationToken>();
                return expected;
            });
        var handler = BuildHandler(client);
        using var cts = new CancellationTokenSource();

        // Act.
        var result = await handler.Handle(
            new SendChatFeedbackCommand("e1", "t1", 5, "Muy útil", "paciente-123"),
            cts.Token
        );

        // Assert.
        Assert.Equal(expected, result);
        Assert.NotNull(capturedDto);
        Assert.Equal("e1", capturedDto!.ExecutionId);
        Assert.Equal("t1", capturedDto.ThreadId);
        Assert.Equal(5, capturedDto.Rating);
        Assert.Equal("Muy útil", capturedDto.Comment);
        Assert.Equal("paciente-123", capturedUserId);
        Assert.Equal(cts.Token, capturedCt);
        await client
            .Received(1)
            .SendFeedbackAsync(Arg.Any<ChatFeedbackRequestDto>(), "paciente-123", cts.Token);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Validator_RatingFueraDeRango_Falla(int rating)
    {
        var validator = new SendChatFeedbackCommandValidator();
        var result = validator.Validate(
            new SendChatFeedbackCommand("e1", "t1", rating, null, "u1")
        );
        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.PropertyName == nameof(SendChatFeedbackCommand.Rating)
        );
    }

    [Fact]
    public void Validator_RatingEnLimites_Pasa()
    {
        var validator = new SendChatFeedbackCommandValidator();
        Assert.True(
            validator.Validate(new SendChatFeedbackCommand("e1", "t1", 1, null, "u1")).IsValid
        );
        Assert.True(
            validator.Validate(new SendChatFeedbackCommand("e1", "t1", 5, null, "u1")).IsValid
        );
    }

    [Theory]
    [InlineData("", "t1", "u1")]
    [InlineData("e1", "", "u1")]
    [InlineData("e1", "t1", "")]
    public void Validator_IdsRequeridos_Falla(string executionId, string threadId, string userId)
    {
        var validator = new SendChatFeedbackCommandValidator();
        var result = validator.Validate(
            new SendChatFeedbackCommand(executionId, threadId, 4, null, userId)
        );
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_ComentarioMuyLargo_Falla()
    {
        var validator = new SendChatFeedbackCommandValidator();
        var result = validator.Validate(
            new SendChatFeedbackCommand("e1", "t1", 4, new string('x', 2001), "u1")
        );
        Assert.False(result.IsValid);
    }
}
