using CoppAddresd.Auth.Entities;

namespace CoppAddresd.Auth.Interfaces;

/// <summary>
/// Ciclo de vida de las invitaciones de primer acceso: crear, validar,
/// aceptar (establecer password), reenviar y revocar. El token en claro solo
/// existe en la respuesta del creador/reenvío (o en el correo); en BD se
/// guarda su hash.
/// </summary>
public interface IInvitationService
{
    /// <summary>Crea la invitación para un usuario (revoca pendientes previas). Devuelve (invitación, token en claro).</summary>
    Task<(bool Success, string? Error, Invitation? Invitation, string? Token)> CreateAsync(
        Guid userId,
        Guid? createdBy,
        CancellationToken ct = default);

    /// <summary>Valida el token sin consumirlo (para la página de aceptación).</summary>
    Task<(bool Valid, string? Error, Invitation? Invitation)> ValidateAsync(
        string token,
        CancellationToken ct = default);

    /// <summary>Acepta la invitación: valida, establece el password y marca usada.</summary>
    Task<(bool Success, string? Error)> AcceptAsync(
        string token,
        string password,
        CancellationToken ct = default);

    /// <summary>Reenvía: revoca la pendiente y crea una nueva. Devuelve (invitación, token en claro).</summary>
    Task<(bool Success, string? Error, Invitation? Invitation, string? Token)> ResendAsync(
        Guid invitationId,
        Guid? createdBy,
        CancellationToken ct = default);

    Task<(bool Success, string? Error)> RevokeAsync(
        Guid invitationId,
        CancellationToken ct = default);
}
