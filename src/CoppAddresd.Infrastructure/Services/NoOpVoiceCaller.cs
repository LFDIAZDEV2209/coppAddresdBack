using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Implementación <c>Noop</c> de <see cref="IVoiceCaller"/>: no llama, solo
/// deja traza en el log y simula una llamada aceptada. Es el proveedor por
/// defecto en desarrollo mientras no se configure Twilio Voice. El despachador
/// SOS la reporta como <c>NoConfigurado</c> (IsConfigured=false), igual que el
/// canal SMS con NoOpSmsSender.
/// </summary>
public sealed class NoOpVoiceCaller(ILogger<NoOpVoiceCaller> logger) : IVoiceCaller
{
    /// <inheritdoc />
    public string Provider => "noop";

    /// <inheritdoc />
    public bool IsConfigured => false;

    /// <inheritdoc />
    public Task<VoiceCallResult> CallAsync(
        string phoneNumber,
        string sayText,
        string language,
        CancellationToken ct = default
    )
    {
        logger.LogInformation(
            "[VOICE Noop] Llamada simulada a {PhoneNumber} ({Language}): {SayText}",
            phoneNumber,
            language,
            sayText
        );

        return Task.FromResult(
            new VoiceCallResult(true, $"noop-{Guid.NewGuid():N}", null)
        );
    }
}
