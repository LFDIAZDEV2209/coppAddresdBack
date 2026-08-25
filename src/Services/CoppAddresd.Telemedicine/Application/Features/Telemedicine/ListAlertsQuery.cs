using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Bandeja de alertas del usuario autenticado. Un profesional (sin
/// <c>Telemedicine.AlertsView</c>) ve solo sus propias alertas (resuelto por
/// identidad del JWT); un usuario con <c>AlertsView</c> ve la bandeja global.
/// Paginada, con recuento de no leídas (badge).
/// </summary>
public sealed record ListAlertsQuery(
    Guid UserId,
    bool HasAlertsView,
    bool UnreadOnly,
    int Page,
    int PageSize) : IRequest<PaginatedAlertsResult>;

public sealed class ListAlertsQueryHandler(
    IAlertRepository alerts,
    IAppointmentReferenceDataService referenceData)
    : IRequestHandler<ListAlertsQuery, PaginatedAlertsResult>
{
    public async Task<PaginatedAlertsResult> Handle(ListAlertsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize == 0 ? 20 : request.PageSize, 1, 100);

        var recipientUserId = await AlertSupport.ResolveRecipientUserIdAsync(
            referenceData, request.UserId, request.HasAlertsView, ct);

        IReadOnlyList<TelemedicineAlert> items;
        int total;
        int unread;

        if (recipientUserId is null)
        {
            (items, total) = await alerts.ListAllAsync(request.UnreadOnly, page, pageSize, ct);
            unread = await alerts.CountUnreadGlobalAsync(ct);
        }
        else
        {
            (items, total) = await alerts.ListForUserAsync(
                recipientUserId.Value, request.UnreadOnly, page, pageSize, ct);
            unread = await alerts.CountUnreadAsync(recipientUserId.Value, ct);
        }

        return new PaginatedAlertsResult(
            items.Select(a => new TelemedicineAlertDto(
                a.Id,
                a.Type,
                a.Severity,
                a.Title,
                a.Body,
                a.RelatedAppointmentId,
                a.ReadAt,
                a.CreatedAt)).ToList(),
            total,
            unread,
            page,
            pageSize,
            (int)Math.Ceiling(total / (double)pageSize));
    }
}
