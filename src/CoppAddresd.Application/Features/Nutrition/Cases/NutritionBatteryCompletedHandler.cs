using CoppAddresd.Application.Features.HealthTests.Events;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Nutrition.Cases;

/// <summary>Reserva el trabajo antes de volver; el worker invoca Generate fuera del commit clínico.</summary>
public sealed class NutritionBatteryCompletedHandler(INutritionDraftWorkflow workflow) : INotificationHandler<BatteryCompletedEvent>
{
    public async Task Handle(BatteryCompletedEvent notification, CancellationToken ct)
        => await workflow.QueueAsync(notification.PatientId, notification.BatteryAssignmentId, ct);
}
