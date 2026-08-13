namespace CoppAddresd.Auth.Entities;

/// <summary>
/// Aplicación del ecosistema (ERP, App móvil, ...). Determina a qué
/// aplicaciones puede acceder un usuario vía <see cref="UserApplication"/>.
/// El código de la aplicación es el audience (`aud`) del JWT.
/// </summary>
public class Application
{
    public Guid Id { get; set; }

    /// <summary>Identificador estable y único, usado como `aud` del JWT (p. ej. "erp", "app").</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<UserApplication> UserApplications { get; set; } = [];
}