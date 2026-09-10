using CoppAddresd.Community.Entities;

namespace CoppAddresd.Community.GraphQL.Types;

/// <summary>Filtros de la query clubs (contrato D1: categoría, búsqueda, visibilidad).</summary>
public sealed record ClubFilterInput(
    string? Category,
    string? Search,
    ClubVisibility? Visibility);

/// <summary>Input de creación/edición de club.</summary>
public sealed record ClubInput(
    string? Name = null,
    string? Slug = null,
    string? Description = null,
    string[]? Rules = null,
    string[]? Objectives = null,
    string? Category = null,
    string[]? Tags = null,
    string? CoverKey = null,
    string? LogoKey = null,
    ClubVisibility Visibility = ClubVisibility.Publico,
    int? MaxMembers = null);

/// <summary>Input de creación/edición de publicación en un club.</summary>
public sealed record ClubPostInput(
    string Body,
    PostType? Type = null,
    ClubPostVisibility? Visibility = null,
    bool? Pinned = null,
    bool? Featured = null,
    DateTime? ScheduledFor = null,
    string? ImageKey = null);

/// <summary>Input de creación de evento de club.</summary>
public sealed record ClubEventInput(
    string Title,
    string Description,
    ClubEventType Type,
    DateTime StartsAt,
    DateTime EndsAt,
    string? Location = null,
    string? MeetingUrl = null,
    int? MaxAttendees = null);

/// <summary>Input de programación de live de club.</summary>
public sealed record LiveSessionInput(
    string Title,
    DateTime ScheduledStartAt,
    Guid? EventId = null,
    string? EmbedUrl = null,
    IReadOnlyList<Guid>? Speakers = null);

/// <summary>Reporte de moderación de un club (mezcla PostReport y CommentReport).</summary>
public sealed record ModerationReportDto(
    Guid Id,
    Guid ClubId,
    string TargetType,   // "POST" | "COMENTARIO"
    Guid TargetId,
    Guid ReporterProfileId,
    string Reason,
    string Status,       // PENDIENTE | RESUELTO | IGNORADO
    DateTime CreatedAt);

/// <summary>Analítica de un club (derivada de la actividad almacenada).</summary>
public sealed record ClubAnalyticsDto(
    int ActiveMembers,
    int WeeklyGrowth,
    double Engagement,
    IReadOnlyList<Post> TopPosts,
    double Retention,
    IReadOnlyList<ClubEvent> EventParticipation);