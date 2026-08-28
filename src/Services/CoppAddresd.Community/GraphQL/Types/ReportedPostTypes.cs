using CoppAddresd.Community.Entities;

namespace CoppAddresd.Community.GraphQL.Types;

/// <summary>DTO que representa un post reportado con sus reportes asociados.</summary>
public sealed record ReportedPost
{
    /// <summary>ID de la publicación reportada.</summary>
    public Guid PostId { get; init; }

    /// <summary>Publicación completa.</summary>
    public Post Post { get; init; } = null!;

    /// <summary>Cantidad total de reportes para esta publicación.</summary>
    public int ReportCount { get; init; }

    /// <summary>Lista de reportes asociados a la publicación.</summary>
    public IReadOnlyList<ReportDto> Reports { get; init; } = [];
}

/// <summary>DTO que representa un reporte individual.</summary>
public sealed record ReportDto
{
    /// <summary>ID del reporte.</summary>
    public Guid Id { get; init; }

    /// <summary>ID de la publicación reportada.</summary>
    public Guid PostId { get; init; }

    /// <summary>Motivo del reporte.</summary>
    public string Reason { get; init; } = default!;

    /// <summary>Detalles adicionales del reporte.</summary>
    public string? Details { get; init; }

    /// <summary>Fecha de creación del reporte.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Perfil del usuario que reportó.</summary>
    public ReportedByProfile? ReportedBy { get; init; }
}

/// <summary>Información básica del perfil que realizó el reporte.</summary>
public sealed record ReportedByProfile
{
    /// <summary>ID del perfil.</summary>
    public Guid Id { get; init; }

    /// <summary>Nombre para mostrar del perfil.</summary>
    public string DisplayName { get; init; } = default!;
}
