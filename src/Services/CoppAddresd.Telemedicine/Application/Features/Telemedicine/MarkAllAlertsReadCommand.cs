using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>Marca como leídas todas las alertas de la bandeja del usuario (o todas, si es admin). Devuelve cuántas se marcaron.</summary>
public sealed record MarkAllAlertsReadCommand(Guid UserId, bool HasAlertsView)
    : IRequest<int>;

public sealed class MarkAllAlertsReadCommandHandler(
    IAlertRepository alerts,
    IAppointmentReferenceDataService referenceData)
    : IRequestHandler<MarkAllAlertsReadCommand, int>
{
    public async Task<int> Handle(MarkAllAlertsReadCommand request, CancellationToken ct)
    {
        var recipientUserId = await AlertSupport.ResolveRecipientUserIdAsync(
            referenceData, request.UserId, request.HasAlertsView, ct);

        return recipientUserId is null
            ? await alerts.MarkAllReadGlobalAsync(ct)
            : await alerts.MarkAllReadAsync(recipientUserId.Value, ct);
    }
}
