using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Implementación <c>Noop</c> de <see cref="ISmsSender"/> (SPEC A13): no envía
/// SMS reales, solo deja traza en el log y simula una entrega correcta. Es el
/// proveedor por defecto en desarrollo mientras no se configure un proveedor
/// real (Twilio Messages, SNS, etc.). El mensaje igualmente queda registrado en
/// <c>app.health_test_notifications</c> con <c>provider = "noop"</c>.
/// </summary>
public sealed class NoOpSmsSender(ILogger<NoOpSmsSender> logger) : ISmsSender
{
    /// <inheritdoc />
    public string Provider => "noop";

    /// <inheritdoc />
    public bool IsConfigured => false;

    /// <inheritdoc />
    public Task<SmsSendResult> SendAsync(
        string phoneNumber,
        string body,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "[SMS Noop] Envío simulado a {PhoneNumber}: {Body}",
            phoneNumber,
            body
        );

        return Task.FromResult(
            new SmsSendResult(true, $"noop-{Guid.NewGuid():N}", null)
        );
    }
}
