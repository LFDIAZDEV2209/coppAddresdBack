namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Token de dispositivo registrado para notificaciones push (FCM). El
/// vínculo con el usuario es por <see cref="UserId"/> (auth.Users, FK creada
/// por SQL en la migración). Un usuario puede tener varios dispositivos, pero
/// nunca el mismo token duplicado (índice único sobre (UserId, Token)).
/// </summary>
public sealed class DeviceToken
{
    public Guid Id { get; set; }

    /// <summary>Id del usuario en <c>auth.Users</c> (FK por SQL).</summary>
    public Guid UserId { get; set; }

    /// <summary>Token del dispositivo para FCM (string opaco, sin PHI).</summary>
    public string Token { get; set; } = default!;

    /// <summary>Plataforma del dispositivo ("android" | "ios").</summary>
    public string Platform { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }
}
