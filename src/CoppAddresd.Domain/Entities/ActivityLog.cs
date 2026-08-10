using System.Text.Json;
using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Entrada del activity log de auditoría (solo lectura).
/// La escritura la realiza el trigger PostgreSQL <c>audit.audit_trigger_function</c>
/// dentro de la transacción de la operación original; EF Core nunca la inserta.
/// </summary>
public sealed class ActivityLog
{
    public Guid Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Operación registrada: INSERT, UPDATE o DELETE.</summary>
    public AuditAction Action { get; set; }

    public string SchemaName { get; set; } = default!;

    public string TableName { get; set; } = default!;

    /// <summary>Valor serializado de la PK de la fila afectada (texto genérico para uuid/int/compuestas).</summary>
    public string RecordId { get; set; } = default!;

    /// <summary>Origen del actor: SYSTEM, ANONYMOUS o USER (futuro Identity).</summary>
    public AuditActorType ActorType { get; set; }

    /// <summary>Futuro AspNetUsers.Id. NULL mientras no exista Identity. Sin FK aún.</summary>
    public Guid? UserId { get; set; }

    public string? UserEmail { get; set; }

    public string? UserRole { get; set; }

    public string? IpAddress { get; set; }

    public string? RequestId { get; set; }

    public string? CorrelationId { get; set; }

    /// <summary>Estado previo de la fila (DELETE y UPDATE).</summary>
    public JsonElement? OldData { get; set; }

    /// <summary>Estado posterior de la fila (INSERT y UPDATE).</summary>
    public JsonElement? NewData { get; set; }

    /// <summary>Solo columnas cuyo valor cambió (UPDATE).</summary>
    public JsonElement? ChangedData { get; set; }

    /// <summary>Reservado para metadatos futuros.</summary>
    public JsonElement? Metadata { get; set; }
}
