namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Cliente hacia el endpoint interno del Auth Service
/// (<c>GET /api/auth/internal/users-by-role</c>, X-Internal-Key) que devuelve
/// los userIds con un rol asignado (global o scoped). Lo usa el directorio de
/// profesionales para filtrar por rol sin leer el schema auth.
/// </summary>
public interface IAuthUsersByRoleClient
{
    /// <summary>
    /// UserIds con el rol (global o scoped). Resultado cacheado 60 s keyed por
    /// roleId: los roles cambian poco y el filtro tolera esa latencia.
    /// Fallo del Auth Service → lista vacía (nunca 500 al cliente).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetUserIdsByRoleAsync(Guid roleId, CancellationToken ct = default);
}
