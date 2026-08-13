namespace CoppAddresd.Domain.Enums;

/// <summary>
/// Ciclo de vida de un medio. Solo los medios <see cref="Published"/> se
/// sirven a los pacientes; Draft/Archived son estados administrativos del ERP.
/// </summary>
public enum MediaStatus
{
    Draft = 1,
    Published = 2,
    Archived = 3,
}
