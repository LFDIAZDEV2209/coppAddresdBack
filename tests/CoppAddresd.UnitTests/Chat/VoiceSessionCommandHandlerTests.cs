using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Chat;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Chat;

/// <summary>
/// Handler <c>VoiceSessionCommandHandler</c> (sesiones de voz ElevenLabs):
/// invoca al cliente IA con el DTO correcto, propaga el
/// <c>CancellationToken</c>, valida el comando y jamás registra el signed URL
/// (credencial temporal del paciente).
/// </summary>
public sealed class VoiceSessionCommandHandlerTests
{
    private static VoiceSessionCommandHandler BuildHandler(IAiServiceClient client) =>
        new(client, NullLogger<VoiceSessionCommandHandler>.Instance);

    [Fact]
    public async Task Handle_InvocaAlClienteConDtoYUserIdYPropagaCt()
    {
        // Arrange.
        var client = Substitute.For<IAiServiceClient>();
        VoiceSessionInternalRequest? capturedDto = null;
        CancellationToken capturedCt = default;
        var expected = new VoiceSessionResponseDto(
            "wss://signed.example/one-time",
            "agent_4501m3qqzq0ne7qtpcf3p2wkec1a",
            "conv_123"
        );
        client
            .CreateVoiceSessionAsync(
                Arg.Any<VoiceSessionInternalRequest>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(callInfo =>
            {
                capturedDto = callInfo.Arg<VoiceSessionInternalRequest>();
                capturedCt = callInfo.Arg<CancellationToken>();
                return expected;
            });
        var handler = BuildHandler(client);
        using var cts = new CancellationTokenSource();

        // Act.
        var result = await handler.Handle(
            new VoiceSessionCommand("paciente-123", "p-1", "proactive-paciente-123"),
            cts.Token
        );

        // Assert.
        Assert.Equal(expected, result);
        Assert.NotNull(capturedDto);
        Assert.Equal("paciente-123", capturedDto!.UserId);
        Assert.Equal("p-1", capturedDto.PatientId);
        Assert.Equal("proactive-paciente-123", capturedDto.ThreadId);
        Assert.Equal(cts.Token, capturedCt);
        await client
            .Received(1)
            .CreateVoiceSessionAsync(Arg.Any<VoiceSessionInternalRequest>(), cts.Token);
    }

    [Fact]
    public void Validator_UserIdVacio_Falla()
    {
        var validator = new VoiceSessionCommandValidator();
        var result = validator.Validate(new VoiceSessionCommand("", null, null));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Usuario no identificado"));
    }

    [Fact]
    public void Validator_ThreadOpcional_Pasa()
    {
        var validator = new VoiceSessionCommandValidator();
        var result = validator.Validate(new VoiceSessionCommand("u1", "p1", "proactive-u-1"));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_ThreadMuyLargo_Falla()
    {
        var validator = new VoiceSessionCommandValidator();
        var result = validator.Validate(new VoiceSessionCommand("u1", null, new string('x', 201)));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("ThreadId"));
    }
}
