namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>Claim y backoff durables de una batería cuyo hook requiere recuperación.</summary>
public sealed class ProgramAssignmentRecovery
{
    public Guid BatteryAssignmentId { get; set; }
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
