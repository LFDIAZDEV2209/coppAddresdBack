namespace CoppAddresd.Domain.Enums;

/// <summary>
/// Operación registrada en el activity log. Los valores <c>Insert/Update/Delete</c>
/// coinciden con <c>TG_OP</c> del trigger; <c>AdaptationApplied</c> es un evento
/// semántico explícito del módulo Progreso del Programa (SPEC §6.8, AC-17) que
/// escribe el repositorio por SQL parametrizado y que exige
/// <c>audit.activity_logs.action</c> con <c>varchar(32)</c>.
/// </summary>
public enum AuditAction
{
    Insert = 1,
    Update = 2,
    Delete = 3,
    AdaptationApplied = 4,
}
