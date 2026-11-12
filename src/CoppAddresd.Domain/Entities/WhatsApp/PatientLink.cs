namespace CoppAddresd.Domain.Entities.WhatsApp;

/// <summary>Identidad del canal independiente del schema clínico; sin FK entre módulos.</summary>
public sealed class PatientLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid? UserId { get; set; }
    public string WaId { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string Language { get; set; } = "es";
    public bool OptOut { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
