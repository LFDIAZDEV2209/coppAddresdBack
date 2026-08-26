using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>Marca una alerta de la bandeja como leída. Resultado: <c>true</c> si existía y estaba sin leer.</summary>
public sealed record MarkAlertReadCommand(Guid AlertId, Guid UserId, bool HasAlertsView)
    : IRequest<bool>;

public sealed class MarkAlertReadCommandHandler(
    IAlertRepository alerts,
    IAppointmentReferenceDataService referenceData)
    : IRequestHandler<MarkAlertReadCommand, bool>
{
    public async Task<bool> Handle(MarkAlertReadCommand request, CancellationToken ct)
    {
        var recipientUserId = await AlertSupport.ResolveRecipientUserIdAsync(
            referenceData, request.UserId, request.HasAlertsView, ct);

        // Admin: marca cualquier alerta por id. Profesional: solo las suyas.
        return recipientUserId is null
            ? await alerts.MarkReadByIdAsync(request.AlertId, ct)
            : await alerts.MarkReadAsync(request.AlertId, recipientUserId.Value, ct);
    }
}
