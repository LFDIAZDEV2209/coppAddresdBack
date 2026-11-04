using MediatR;

namespace CoppAddresd.Application.Features.HealthTests.Events;

/// <summary>Se publica en la request tras confirmar los resultados clínicos; nunca dentro del commit.</summary>
public sealed record BatteryCompletedEvent(Guid PatientId, Guid BatteryAssignmentId) : INotification;
