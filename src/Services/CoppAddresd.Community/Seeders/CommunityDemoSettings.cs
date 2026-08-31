namespace CoppAddresd.Community.Seeders;

/// <summary>Configuración del contenido demo de la comunidad (desarrollo).</summary>
public sealed class CommunityDemoSettings
{
    public const string SectionName = "CommunityDemo";

    /// <summary>Si está deshabilitado, el seeder de contenido no crea nada (no-op).</summary>
    public bool Enabled { get; set; }
}