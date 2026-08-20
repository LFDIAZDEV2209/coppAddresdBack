using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>Alerta de la bandeja (materialización de un evento de dominio).</summary>
public sealed record TelemedicineAlertDto(
    Guid Id,
    AlertType Type,
    AlertSeverity Severity,
    string Title,
    string? Body,
    Guid? RelatedAppointmentId,
    DateTimeOffset? ReadAt,
    DateTime CreatedAt);

/// <summary>Bandeja de alertas paginada (para la vista profesional y la administrativa).</summary>
public sealed record PaginatedAlertsResult(
    IReadOnlyList<TelemedicineAlertDto> Items,
    int Total,
    int Unread,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>Resumen de la bandeja (badge de la UI sin traer la lista completa).</summary>
public sealed record AlertsSummaryDto(int Unread);
