using CoppAddresd.Community.Entities;

namespace CoppAddresd.Community.GraphQL.Types;

/// <summary>DTO que representa un comentario reportado con sus reportes asociados.</summary>
public sealed record ReportedComment
{
    /// <summary>ID del comentario reportado.</summary>
    public Guid CommentId { get; init; }

    /// <summary>Comentario completo.</summary>
    public Comment Comment { get; init; } = null!;

    /// <summary>Publicación padre (para contexto de moderación).</summary>
    public Post? Post { get; init; }

    /// <summary>Cantidad total de reportes para este comentario.</summary>
    public int ReportCount { get; init; }

    /// <summary>Lista de reportes asociados al comentario.</summary>
    public IReadOnlyList<CommentReportDto> Reports { get; init; } = [];
}

/// <summary>DTO que representa un reporte individual de comentario.</summary>
public sealed record CommentReportDto
{
    /// <summary>ID del reporte.</summary>
    public Guid Id { get; init; }

    /// <summary>ID del comentario reportado.</summary>
    public Guid CommentId { get; init; }

    /// <summary>Motivo del reporte.</summary>
    public string Reason { get; init; } = default!;

    /// <summary>Detalles adicionales del reporte.</summary>
    public string? Details { get; init; }

    /// <summary>Fecha de creación del reporte.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Perfil del usuario que reportó.</summary>
    public ReportedByProfile? ReportedBy { get; init; }
}
