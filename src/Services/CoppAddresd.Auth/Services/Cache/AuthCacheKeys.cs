namespace CoppAddresd.Auth.Services.Cache;

/// <summary>
/// Convención de claves de caché del Auth Service: el prefijo de servicio lo
/// añade la implementación (<c>auth:...</c>); aquí se definen las claves por
/// dominio con su versión. Los datos cacheados son catálogos de autorización
/// (mapeo código→id y códigos por rol) — NUNCA tokens, contraseñas ni OTPs.
/// </summary>
public static class AuthCacheKeys
{
    /// <summary>Versión actual de las claves (bump para invalidar el dominio entero).</summary>
    public const string Version = "v1";

    /// <summary>Catálogo de permisos: código → id es inmutable en runtime (solo seeders).</summary>
    public static readonly TimeSpan PermissionIdTtl = TimeSpan.FromHours(24);

    /// <summary>
    /// Códigos por rol: mutables (assign/remove). TTL corto alineado con la
    /// vida del access token (15 min) como cota de staleness máxima; la
    /// invalidación activa ocurre en AssignToRole/RemoveFromRole.
    /// </summary>
    public static readonly TimeSpan RoleCodesTtl = TimeSpan.FromMinutes(15);

    /// <summary>Clave del mapeo código de permiso → id: <c>permits:code:{code}:v1</c>.</summary>
    public static string PermissionId(string code) => $"permits:code:{code}:{Version}";

    /// <summary>Clave de los códigos de permisos de un rol: <c>roles:{roleId}:codes:v1</c>.</summary>
    public static string RoleCodes(Guid roleId) => $"roles:{roleId}:codes:{Version}";
}
