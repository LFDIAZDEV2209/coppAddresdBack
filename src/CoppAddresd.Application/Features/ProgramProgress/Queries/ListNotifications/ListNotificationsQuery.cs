using CoppAddresd.Application.Features.ProgramProgress.DTOs.Notifications;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.ListNotifications;

/// <summary>
/// Centro de notificaciones gamificadas del paciente autenticado (SPEC §20, D):
/// log de hitos de racha / racha del nutracéutico / subida de nivel / día
/// perfecto, paginado (default 20, orden descendente por fecha), con
/// <c>readAt</c> por fila y el <c>unreadCount</c> total para el badge del móvil.
///
/// El <c>patientId</c> SIEMPRE llega resuelto de la identidad del JWT por la
/// capa API (nunca del body): es la base del anti-IDOR (AC-11) — un cruce entre
/// pacientes devuelve 404, nunca 403.
/// </summary>
public sealed record ListNotificationsQuery(Guid PatientId, int Page = 1, int PageSize = 20)
    : IRequest<PaginatedNotificationsResult>;

public sealed class ListNotificationsQueryHandler(
    INotificationLogRepository repository) : IRequestHandler<ListNotificationsQuery, PaginatedNotificationsResult>
{
    public Task<PaginatedNotificationsResult> Handle(
        ListNotificationsQuery request, CancellationToken ct)
        => repository.ListAsync(request.PatientId, request.Page, request.PageSize, ct);
}