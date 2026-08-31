namespace CoppAddresd.Auth.Configuration;

/// <summary>Configuración del seeder de usuarios demo de la comunidad.</summary>
public class CommunityDemoSettings
{
    public const string SectionName = "CommunityDemo";

    /// <summary>Si está deshabilitado, el seeder no crea nada (no-op).</summary>
    public bool Enabled { get; set; }

    /// <summary>Contraseña compartida de todos los usuarios demo (login con documento + contraseña).</summary>
    public string Password { get; set; } = string.Empty;
}