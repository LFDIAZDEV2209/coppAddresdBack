using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Features.Notifications;

/// <summary>Token de dispositivo registrado para notificaciones push.</summary>
public record DeviceTokenDto(
    Guid Id,
    Guid UserId,
    string Token,
    string Platform,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string Language = "es")
{
    public static DeviceTokenDto FromEntity(DeviceToken entity) => new(
        entity.Id,
        entity.UserId,
        entity.Token,
        entity.Platform,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.Language);
}

/// <summary>Payload de registro de un dispositivo (POST /api/v1/notifications/devices).</summary>
public record RegisterDeviceTokenRequest(string Token, string Platform, string? Language = null);