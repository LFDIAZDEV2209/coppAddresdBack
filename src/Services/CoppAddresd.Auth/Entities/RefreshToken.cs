namespace CoppAddresd.Auth.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// Aplicación para la cual se emitió el token. El access token rotado en
    /// <c>refresh</c> conserva el mismo `aud` de esta aplicación. Nullable solo
    /// por compatibilidad con tokens emitidos antes del binding por aplicación
    /// (el seeder los retro-asigna al ERP al iniciar).
    /// </summary>
    public Guid? ApplicationId { get; set; }
    /// <summary>Versión del acceso al emitir; protege incluso ante emisión concurrente con suspensión.</summary>
    public long ApplicationSessionVersion { get; set; }

    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsRevoked && !IsExpired;

    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Application? Application { get; set; }
    public virtual RefreshToken? ReplacedByToken { get; set; }
}
