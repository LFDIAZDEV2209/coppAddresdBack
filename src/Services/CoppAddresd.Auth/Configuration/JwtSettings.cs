namespace CoppAddresd.Auth.Configuration;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Audiencias (`aud`) aceptadas al validar tokens. Si la lista está vacía
    /// se usan los códigos de aplicación conocidos ("app", "erp").
    /// </summary>
    public List<string> ValidAudiences { get; set; } = [];

    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
