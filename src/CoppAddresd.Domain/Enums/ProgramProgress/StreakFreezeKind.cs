namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Tipo de movimiento de un congelamiento de racha: otorgado, consumido
/// (cubre un día perdido) o expirado sin uso.
/// </summary>
public enum StreakFreezeKind
{
    Granted = 1,
    Consumed = 2,
    Expired = 3,
}