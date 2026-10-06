namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Estado de una cuenta de Auth leído por correo (endpoint interno).
/// <see cref="HasPassword"/> true = la cuenta ya puede iniciar sesión; false =
/// está pendiente de invitación/activación.
/// </summary>
public record AccountLookupResult(bool Exists, bool IsActive, bool HasPassword);

/// <summary>
/// Cliente de los endpoints internos del Auth Service para leer el estado de
/// una cuenta por correo. Best-effort: si el Auth no responde devuelve null
/// (el preflight del alta no debe bloquearse por una lectura informativa).
/// </summary>
public interface IAuthUsersLookupClient
{
    Task<AccountLookupResult?> LookupByEmailAsync(
        string email,
        CancellationToken ct = default
    );
}
