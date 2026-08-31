using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio del módulo de notificaciones push: registro y desregistro de
/// tokens de dispositivos (FCM). Operaciones acotadas al par (UserId, Token).
/// </summary>
public interface IDeviceTokenRepository
{
    /// <summary>Devuelve el token registrado para el par (userId, token), o null.</summary>
    Task<DeviceToken?> GetByUserAndTokenAsync(Guid userId, string token, CancellationToken ct = default);

    /// <summary>Devuelve el token registrado por valor de token (desregistro sin userId), o null.</summary>
    Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Devuelve todos los tokens registrados del usuario (fan-out de push).
    /// </summary>
    Task<IReadOnlyList<DeviceToken>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Inserta un nuevo token.</summary>
    Task AddAsync(DeviceToken deviceToken, CancellationToken ct = default);

    /// <summary>Actualiza un token existente (platform/updatedAt).</summary>
    Task UpdateAsync(DeviceToken deviceToken, CancellationToken ct = default);

    /// <summary>Elimina un token existente.</summary>
    Task DeleteAsync(DeviceToken deviceToken, CancellationToken ct = default);

    /// <summary>
    /// Elimina un token por valor (token obsoleto UNREGISTERED detectado en un
    /// envío FCM). Devuelve true si existía.
    /// </summary>
    Task<bool> DeleteByTokenAsync(string token, CancellationToken ct = default);
}