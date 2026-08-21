namespace CoppAddresd.Telemedicine.Application.VideoProvider;

/// <summary>
/// Tipo de sala del proveedor. Mapeado a cada proveedor concreto por su
/// implementación (el dominio no conoce la nomenclatura del proveedor).
/// </summary>
public enum VideoRoomType
{
    Group,
    GroupSmall,
    PeerToPeer
}

/// <summary>Petición para crear una sala virtual en el proveedor.</summary>
public sealed record RoomRequest(
    string RoomName,
    VideoRoomType Type,
    int MaxParticipants,
    DateTimeOffset? EndTime,
    string? StatusCallbackUrl);

/// <summary>
/// Información devuelta por el proveedor. <c>Status</c> es el estado crudo del
/// proveedor (p. ej. "in-progress"): la capa de aplicación lo mapea al estado
/// del dominio <see cref="CoppAddresd.Telemedicine.Domain.Enums.VirtualRoomStatus"/>.
/// </summary>
public sealed record RoomInfo(
    string ProviderRoomSid,
    string ProviderRoomName,
    string Status,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? EndedAt,
    int? DurationSeconds);

/// <summary>Petición para generar un token de acceso a una sala.</summary>
public sealed record AccessTokenRequest(
    string Identity,
    string RoomName,
    int TtlSeconds);

/// <summary>Participante de una sala según el proveedor.</summary>
public sealed record ParticipantInfo(
    string ParticipantSid,
    string Identity,
    bool IsConnected,
    DateTimeOffset? ConnectedAt,
    DateTimeOffset? DisconnectedAt);

/// <summary>
/// Petición de validación de firma de webhook del proveedor. El proveedor
/// conoce el mecanismo de firma (p. ej. X-Twilio-Signature); el dominio solo
/// necesita saber si la petición es legítima.
/// </summary>
public sealed record WebhookValidationRequest(
    string Url,
    string Signature,
    IReadOnlyDictionary<string, string> FormParams);

/// <summary>
/// Abstracción del proveedor de video. El dominio de telemedicina depende de
/// esta interfaz, nunca de Twilio ni de otro proveedor. Implementaciones:
/// <see cref="CoppAddresd.Telemedicine.Infrastructure.VideoProvider.TwilioVideoProvider"/>
/// y futuros proveedores (cada uno es una clase + configuración, sin tocar el
/// dominio ni los handlers).
/// </summary>
public interface IVideoProvider
{
    /// <summary>Crea una sala. Idempotente por nombre determinista.</summary>
    Task<RoomInfo> CreateRoomAsync(RoomRequest request, CancellationToken ct);

    /// <summary>Obtiene una sala por Sid o nombre. <c>null</c> si no existe.</summary>
    Task<RoomInfo?> GetRoomAsync(string providerRoomSidOrName, CancellationToken ct);

    /// <summary>Completa/finaliza una sala (desconecta participantes).</summary>
    Task CompleteRoomAsync(string providerRoomSid, CancellationToken ct);

    /// <summary>Genera un token de acceso de corta vida para un participante.</summary>
    Task<string> GenerateAccessTokenAsync(AccessTokenRequest request, CancellationToken ct);

    /// <summary>Lista los participantes de una sala.</summary>
    Task<IReadOnlyList<ParticipantInfo>> GetParticipantsAsync(string providerRoomSid, CancellationToken ct);

    /// <summary>Valida la firma de un webhook del proveedor.</summary>
    Task<bool> ValidateWebhookSignatureAsync(WebhookValidationRequest request, CancellationToken ct);
}
