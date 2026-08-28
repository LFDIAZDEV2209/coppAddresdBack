namespace CoppAddresd.Community.Entities;

/// <summary>Estado del perfil de un usuario en la comunidad (sin revisión previa: activo por defecto, baneo posterior).</summary>
public enum ProfileStatus
{
    Active = 1,
    Banned = 2,
}

/// <summary>Perfil público de un usuario de la comunidad (referencia a auth.users).</summary>
public sealed class Profile
{
    public Guid Id { get; set; }

    /// <summary>Id del usuario en el servicio de Auth (auth.users). Nulo para perfiles del sistema.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Indica si el perfil es un perfil de sistema (ej. "Equipo ANTARES") y no está asociado a un usuario de Auth.</summary>
    public bool IsSystem { get; set; }

    public string DisplayName { get; set; } = default!;

    public string? Bio { get; set; }

    /// <summary>Clave del avatar en el storage (text-only por ahora: NULL).</summary>
    public string? AvatarKey { get; set; }

    /// <summary>Clave de la foto de portada en el storage (NULL si aún no hay portada).</summary>
    public string? CoverKey { get; set; }

    public ProfileStatus Status { get; set; } = ProfileStatus.Active;

    public Guid? BannedBy { get; set; }

    public DateTime? BannedAt { get; set; }

    public string? BanReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Región del perfil (catálogo). Almacenado como texto.</summary>
    public ProfileRegion? Region { get; set; }

    /// <summary>Diagnóstico de referencia del perfil (catálogo). Almacenado como texto.</summary>
    public ProfileDiagnosis? Diagnosis { get; set; }

    /// <summary>Semana de onboarding/enfoque en la comunidad (ej. 1, 2, 3…).</summary>
    public int? Week { get; set; }

    /// <summary>Fecha de la última publicación del perfil (para riesgo de inactividad).</summary>
    public DateTimeOffset? LastPostAt { get; set; }

    /// <summary>Fecha de la última actividad registrada del perfil.</summary>
    public DateTimeOffset? LastActiveAt { get; set; }

    /// <summary>Racha actual de días consecutivos con publicación (mantenida por side-effect).</summary>
    public int CurrentStreak { get; set; }

    /// <summary>Mayor racha histórica de días consecutivos con publicación.</summary>
    public int BestStreak { get; set; }

    /// <summary>XP total acumulado del perfil (suma de XpEntry.Amount, mantenido por side-effect).</summary>
    public int XpTotal { get; set; }

    public ICollection<Post> Posts { get; set; } = [];
    public ICollection<Comment> Comments { get; set; } = [];
    public ICollection<Like> Likes { get; set; } = [];
    public ICollection<XpEntry> XpEntries { get; set; } = [];
}
