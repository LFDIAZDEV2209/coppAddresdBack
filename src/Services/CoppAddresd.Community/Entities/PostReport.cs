namespace CoppAddresd.Community.Entities;

/// <summary>Reporte de una publicación por parte de un usuario.</summary>
public sealed class PostReport
{
    public Guid Id { get; set; }

    /// <summary>Publicación reportada.</summary>
    public Guid PostId { get; set; }

    /// <summary>Perfil del usuario que reporta (nullable: SET NULL al eliminar).</summary>
    public Guid? ReportedByProfileId { get; set; }

    /// <summary>Motivo del reporte (máx. 100 caracteres, obligatorio).</summary>
    public string Reason { get; set; } = default!;

    /// <summary>Detalles adicionales del reporte (máx. 500 caracteres, opcional).</summary>
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navegaciones
    public Post? Post { get; set; }
    public Profile? ReportedBy { get; set; }
}
