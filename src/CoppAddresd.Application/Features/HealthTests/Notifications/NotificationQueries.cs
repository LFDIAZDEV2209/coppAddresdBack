using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.HealthTests;
using MediatR;

namespace CoppAddresd.Application.Features.HealthTests.Notifications;

/// <summary>Registro paginado de notificaciones enviadas (auditoría de entregas).</summary>
public record ListNotificationsQuery(
    Guid? AlertId = null,
    Guid? PatientId = null,
    NotificationChannel? Channel = null,
    NotificationStatus? Status = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<PaginatedHealthTestsResult<HealthTestNotificationDto>>;

public sealed class ListNotificationsQueryHandler(
    IHealthTestNotificationRepository repository
) : IRequestHandler<ListNotificationsQuery, PaginatedHealthTestsResult<HealthTestNotificationDto>>
{
    public async Task<PaginatedHealthTestsResult<HealthTestNotificationDto>> Handle(
        ListNotificationsQuery request,
        CancellationToken ct
    )
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, total) = await repository.ListNotificationsAsync(
            request.AlertId,
            request.PatientId,
            request.Channel,
            request.Status,
            request.From,
            request.To,
            page,
            pageSize,
            ct
        );

        return new PaginatedHealthTestsResult<HealthTestNotificationDto>(
            items.Select(HealthTestNotificationDto.FromEntity).ToList(),
            total,
            page,
            pageSize,
            (int)Math.Ceiling(total / (double)pageSize)
        );
    }
}

/// <summary>Datos agregados para los gráficos de la vista de alertas.</summary>
public record GetNotificationChartsQuery(Guid? ProfessionalId = null, int Days = 30)
    : IRequest<HealthTestNotificationChartsDto>;

public sealed class GetNotificationChartsQueryHandler(
    IHealthTestNotificationRepository repository
) : IRequestHandler<GetNotificationChartsQuery, HealthTestNotificationChartsDto>
{
    public async Task<HealthTestNotificationChartsDto> Handle(
        GetNotificationChartsQuery request,
        CancellationToken ct
    )
    {
        var days = Math.Clamp(request.Days, 1, 180);
        var from = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var alertsBySeverity = await repository.CountAlertsBySeverityAsync(
            request.ProfessionalId,
            ct
        );
        var alertsByStatus = await repository.CountAlertsByStatusAsync(request.ProfessionalId, ct);
        var alertsByIndicator = await repository.CountAlertsByIndicatorAsync(
            request.ProfessionalId,
            10,
            ct
        );
        var alertsByDay = await repository.CountAlertsByDayAsync(request.ProfessionalId, days, ct);
        var notificationsByChannel = await repository.CountNotificationsByChannelAsync(
            request.ProfessionalId,
            from,
            null,
            ct
        );
        var notificationsByStatus = await repository.CountNotificationsByStatusAsync(
            request.ProfessionalId,
            from,
            null,
            ct
        );
        var notificationsByDay = await repository.CountNotificationsByDayAsync(
            request.ProfessionalId,
            days,
            ct
        );

        return new HealthTestNotificationChartsDto(
            alertsBySeverity,
            alertsByStatus,
            alertsByIndicator,
            alertsByDay.Select(p => new HealthTestChartPointDto(p.Date.ToString("yyyy-MM-dd"), p.Count)).ToList(),
            notificationsByChannel,
            notificationsByStatus,
            notificationsByDay
                .Select(p => new HealthTestChartPointDto(p.Date.ToString("yyyy-MM-dd"), p.Count))
                .ToList()
        );
    }
}
