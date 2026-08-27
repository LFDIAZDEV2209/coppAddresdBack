using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Alerta clínica derivada de un resultado que satisface una
/// <see cref="HealthTestAlertRule"/>. El destinatario es el paciente (y el
/// profesional asignado lo resuelve el ERP por alcance). Estados:
/// <c>active → reviewing → resolved → closed</c> con registro de quién/cuándo.
/// </summary>
public sealed class HealthTestAlert
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    /// <summary>Resultado que disparó la alerta (para deduplicar por regla).</summary>
    public Guid? ResultId { get; set; }

    public Guid? RuleId { get; set; }

    public HealthTestSeverity Severity { get; set; } = HealthTestSeverity.high;

    public string Title { get; set; } = default!;

    public string? Body { get; set; }

    public HealthTestAlertStatus Status { get; set; } = HealthTestAlertStatus.active;

    /// <summary>Usuario (auth.users) que pasó a reviewing.</summary>
    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    /// <summary>Usuario (auth.users) que resolvió.</summary>
    public Guid? ResolvedBy { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PatientProfile? Patient { get; set; }

    public HealthTestResult? Result { get; set; }

    public HealthTestAlertRule? Rule { get; set; }
}
