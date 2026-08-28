namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Máquina de estados de una recomendación de adaptación:
/// Pending → Approved → Applied (terminal), Pending → Rejected (terminal),
/// y Superseded cuando una recomendación más nueva del mismo tipo la reemplaza.
/// </summary>
public enum AdaptationStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Applied = 4,
    Superseded = 5,
}