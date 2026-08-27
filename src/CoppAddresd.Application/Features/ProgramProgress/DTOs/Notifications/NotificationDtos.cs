namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.Notifications;

/// <summary>
/// Fila del centro de notificaciones gamificadas del programa (SPEC §20, D):
/// shape del <c>GET /api/v1/program/notifications</c>. <c>ReadAt</c> null =
/// pendiente de lectura; <c>Priority</c>/<c>Channel</c> reflejan el log
/// (<c>normal</c>/<c>high</c>/<c>critical</c> y <c>push</c> en MVP).
/// </summary>
public sealed record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Message,
    string Priority,
    string Channel,
    DateTime SentAt,
    DateTime? ReadAt);

/// <summary>
/// Respuesta paginada del centro de notificaciones (SPEC §20, D): la lista de
/// filas ordenada por <c>sent_at</c> descendente, el total de filas del
/// paciente y el <c>UnreadCount</c> para el badge del móvil (independiente de
/// la página actual).
/// </summary>
public sealed record PaginatedNotificationsResult(
    IReadOnlyList<NotificationDto> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages,
    int UnreadCount);