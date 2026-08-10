using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Provee el contexto del actor que origina una operación, para propagarlo
/// al activity log vía variables GUC transaccionales de PostgreSQL
/// (<c>set_config(..., true)</c>). Sin Identity: ActorType = System, UserId = null.
/// </summary>
public interface IAuditActorContext
{
    AuditActorType ActorType { get; }

    Guid? UserId { get; }

    string? UserEmail { get; }

    string? UserRole { get; }

    string? IpAddress { get; }

    string? RequestId { get; }

    string? CorrelationId { get; }
}
