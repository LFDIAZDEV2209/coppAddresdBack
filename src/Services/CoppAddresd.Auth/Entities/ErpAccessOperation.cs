namespace CoppAddresd.Auth.Entities;

/// <summary>Registro durable de una transición de acceso ERP y su proyección al directorio.</summary>
public sealed class ErpAccessOperation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Status { get; set; } = "Inactive";
    public long SessionVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
