namespace CoppAddresd.Auth.Configuration;

public class AuthSettings
{
    public const string SectionName = "Auth";

    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminFirstName { get; set; } = "Admin";
    public string AdminLastName { get; set; } = "System";

    /// <summary>
    /// Clave compartida para endpoints internos (ERP → Auth). Se envía en el
    /// header <c>X-Internal-Key</c>; no es un token de usuario, solo permite
    /// la introspección de autorización. Nunca commitear un valor real.
    /// </summary>
    public string InternalApiKey { get; set; } = string.Empty;
}
