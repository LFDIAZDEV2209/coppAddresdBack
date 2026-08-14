namespace CoppAddresd.Auth.Interfaces;

public interface ITokenInvalidationService
{
    /// <summary>
    /// Invalida los tokens de UN usuario (bump del security stamp).
    /// </summary>
    Task InvalidateUserTokensAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Invalida los tokens de MUCHOS usuarios en UNA sola sentencia
    /// (REQ-INVALID-05): un UPDATE batch por rol/perfil con muchos miembros,
    /// en lugar de un loop con 2 round trips por usuario.
    /// </summary>
    Task InvalidateUsersTokensAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);
}
