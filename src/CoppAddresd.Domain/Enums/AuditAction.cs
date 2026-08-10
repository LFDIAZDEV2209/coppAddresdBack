namespace CoppAddresd.Domain.Enums;

/// <summary>Operación registrada en el activity log. Los valores coinciden con TG_OP del trigger.</summary>
public enum AuditAction
{
    Insert = 1,
    Update = 2,
    Delete = 3,
}
