namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Máquina de estados de una revisión clínica de XP (SPEC §15):
/// <c>pending</c> → <c>approved</c> (se otorga <c>CLINICAL_SIGNIFICANT</c>) o
/// <c>rejected</c> (no se otorga nada). Miembros en minúscula: el valor se
/// persiste tal cual en <c>app.clinical_xp_reviews.status</c> (varchar(20),
/// default 'pending').
/// </summary>
public enum ClinicalXpReviewStatus
{
    pending = 1,
    approved = 2,
    rejected = 3,
}