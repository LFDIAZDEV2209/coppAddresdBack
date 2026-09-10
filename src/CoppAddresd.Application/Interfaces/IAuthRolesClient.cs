namespace CoppAddresd.Application.Interfaces;

/// <summary>Resultado de la consulta de un rol por nombre desde el Auth Service.</summary>
public record AuthRoleLookupResult(Guid Id, string Name, bool IsActive);

/// <summary>
/// Cliente hacia los endpoints internos de roles del Auth Service
/// (X-Internal-Key). El ERP lo usa para resolver roles por nombre
/// al sincronizar scopes por clínica.
/// </summary>
public interface IAuthRolesClient
{
    /// <summary>
    /// Busca un rol por nombre (case-insensitive). Devuelve null si no existe.
    /// </summary>
    Task<AuthRoleLookupResult?> GetRoleByNameAsync(string name, CancellationToken ct = default);
}
