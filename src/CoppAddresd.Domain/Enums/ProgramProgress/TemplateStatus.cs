namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Ciclo de vida de una plantilla de programa.
/// Draft = borrador editable, Active = publicada (se usa en inscripciones),
/// Archived = archivada (inactiva, conserva historia).
/// </summary>
public enum TemplateStatus
{
    Draft = 1,
    Active = 2,
    Archived = 3,
}