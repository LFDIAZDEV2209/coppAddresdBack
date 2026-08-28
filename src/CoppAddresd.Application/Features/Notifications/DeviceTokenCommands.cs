using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Notifications;

/// <summary>
/// Registra (upsert) el token de un dispositivo para el usuario autenticado.
/// Si el par (UserId, Token) ya existe, actualiza plataforma y UpdatedAt;
/// si no, inserta una fila nueva.
/// </summary>
public record RegisterDeviceTokenCommand(Guid UserId, string Token, string Platform)
    : IRequest<DeviceTokenDto>;

public sealed class RegisterDeviceTokenCommandHandler(
    IDeviceTokenRepository repository,
    ILogger<RegisterDeviceTokenCommandHandler> logger) : IRequestHandler<RegisterDeviceTokenCommand, DeviceTokenDto>
{
    public async Task<DeviceTokenDto> Handle(RegisterDeviceTokenCommand request, CancellationToken ct)
    {
        var token = request.Token.Trim();
        var platform = NormalizePlatform(request.Platform);

        var existing = await repository.GetByUserAndTokenAsync(request.UserId, token, ct);

        if (existing is not null)
        {
            existing.Platform = platform;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await repository.UpdateAsync(existing, ct);

            logger.LogInformation("DeviceToken actualizado: userId={UserId}, tokenId={Id}", request.UserId, existing.Id);
            return DeviceTokenDto.FromEntity(existing);
        }

        var deviceToken = new DeviceToken
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Token = token,
            Platform = platform,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await repository.AddAsync(deviceToken, ct);

        logger.LogInformation("DeviceToken registrado: userId={UserId}, tokenId={Id}", request.UserId, deviceToken.Id);
        return DeviceTokenDto.FromEntity(deviceToken);
    }

    /// <summary>
    /// Normaliza la plataforma a la convención del dominio ("android" | "ios").
    /// Valores desconocidos se conservan en minúsculas (el envío FCM los
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
/// autenticado (o por token, si el userId no matchea). Devuelve false si no
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

        // Primero intenta el par exacto (userId, token); si no, intenta por
        // token a secas (el token FCM es único por dispositivo).
        var existing = await repository.GetByUserAndTokenAsync(request.UserId, token, ct)
            ?? await repository.GetByTokenAsync(token, ct);

        if (existing is null)
        {
            return false;
        }

        await repository.DeleteAsync(existing, ct);

        logger.LogInformation("DeviceToken desregistrado: userId={UserId}, tokenId={Id}", request.UserId, existing.Id);
        return true;
    }
}