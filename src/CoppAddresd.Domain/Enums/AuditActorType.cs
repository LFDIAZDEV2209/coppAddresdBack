namespace CoppAddresd.Domain.Enums;

/// <summary>
/// Origen del actor que provocó la operación auditada.
/// Mientras no exista Identity toda operación es SYSTEM o ANONYMOUS.
/// </summary>
public enum AuditActorType
{
    /// <summary>Operación iniciada por el sistema (jobs, migraciones, procesos internos).</summary>
    System = 1,

    /// <summary>Operación sin usuario autenticado.</summary>
    Anonymous = 2,

    /// <summary>Operación de un usuario autenticado (futuro Identity).</summary>
    User = 3,
}
