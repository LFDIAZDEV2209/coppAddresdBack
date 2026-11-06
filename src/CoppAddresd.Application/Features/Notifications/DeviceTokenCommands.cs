using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Notifications;

/// <summary>
/// Registra (upsert) el token de un dispositivo para el usuario autenticado.
/// El token queda asociado solo a esta cuenta; actualiza plataforma/idioma
/// y conserva el idioma de la misma cuenta cuando no se envía.
/// </summary>
public record RegisterDeviceTokenCommand(Guid UserId, string Token, string Platform, string? Language = null)
    : IRequest<DeviceTokenDto>;

public sealed class RegisterDeviceTokenCommandHandler(
    IDeviceTokenRepository repository,
    ILogger<RegisterDeviceTokenCommandHandler> logger) : IRequestHandler<RegisterDeviceTokenCommand, DeviceTokenDto>
{
    public async Task<DeviceTokenDto> Handle(RegisterDeviceTokenCommand request, CancellationToken ct)
    {
        var token = request.Token.Trim();
        var platform = NormalizePlatform(request.Platform);
        var language = request.Language?.Trim().ToLowerInvariant();
        if (language is not null and not "es" and not "en")
            throw new UnprocessableEntityException("El idioma debe ser es o en.");

        var device = await repository.RegisterForUserAsync(request.UserId, token, platform, language, ct);
        logger.LogInformation("DeviceToken registrado: userId={UserId}, tokenId={Id}", request.UserId, device.Id);
        return DeviceTokenDto.FromEntity(device);
    }

    /// <summary>
    /// Normaliza la plataforma a la convención del dominio
    /// ("android" | "ios" | "web"); "web" cubre los tokens FCM de clientes web
    /// (ERP). Valores desconocidos se conservan en minúsculas (el envío FCM los
    /// interpreta en el futuro); se evita null/whitespace.
    /// </summary>
    private static string NormalizePlatform(string platform)
    {
        var normalized = (platform ?? string.Empty).Trim().ToLowerInvariant();
        return normalized.Length == 0 ? "unknown" : normalized;
    }
}

/// <summary>
/// Desregistra el token de un dispositivo: elimina la fila del usuario
/// autenticado; nunca elimina el registro de otra cuenta. Devuelve false si no
/// existía.
/// </summary>
public record UnregisterDeviceTokenCommand(Guid UserId, string Token) : IRequest<bool>;

public sealed class UnregisterDeviceTokenCommandHandler(
    IDeviceTokenRepository repository,
    ILogger<UnregisterDeviceTokenCommandHandler> logger) : IRequestHandler<UnregisterDeviceTokenCommand, bool>
{
    public async Task<bool> Handle(UnregisterDeviceTokenCommand request, CancellationToken ct)
    {
        var token = request.Token.Trim();

        var removed = await repository.UnregisterForUserAsync(request.UserId, token, ct);
        if (!removed) return false;

        logger.LogInformation("DeviceToken desregistrado: userId={UserId}", request.UserId);
        return true;
    }
}