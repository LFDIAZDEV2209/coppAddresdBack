using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>Reutiliza patient_professionals con asignación activa, incluyendo listas y recursos indirectos.</summary>
public sealed class PatientScopeStore(AppDbContext db) : IPatientScopeStore
{
    public IQueryable<Guid> VisiblePatients(Guid? professionalId, bool isAdmin)
        => db.PatientProfiles.AsNoTracking().Where(p => p.DeletedAt == null && (isAdmin
            || professionalId != null && db.PatientProfessionalAssignments.Any(a =>
                a.PatientId == p.Id && a.ProfessionalId == professionalId && a.Status == "Active"))).Select(p => p.Id);

    public Task<bool> ContainsAsync(IQueryable<Guid> patientIds, Guid patientId, CancellationToken ct)
        => patientIds.AnyAsync(p => p == patientId, ct);

    public Task<Guid?> OwnerAsync(PatientResource resource, Guid id, CancellationToken ct)
        => (resource switch
        {
            PatientResource.Enrollment => db.ProgramEnrollments.Where(x => x.Id == id).Select(x => (Guid?)x.PatientId),
            PatientResource.Week => db.ProgramWeeks.Where(x => x.Id == id).Select(x => (Guid?)x.Enrollment!.PatientId),
            PatientResource.Prescription => db.NutritionPrescriptions.Where(x => x.Id == id).Select(x => (Guid?)x.PatientId),
            PatientResource.Cycle => db.NutritionPrescriptions.Where(x => x.CycleId == id).Select(x => (Guid?)x.PatientId).Distinct(),
            PatientResource.Intake => db.NutritionIntakeLogs.Where(x => x.Id == id).Select(x => (Guid?)x.PatientId),
            PatientResource.FoodAnalysis => from a in db.FoodAnalyses where a.AnalysisId == id
                join p in db.PatientProfiles on a.UserId equals p.UserId select (Guid?)p.Id,
            PatientResource.NutritionPlan => db.NutritionPlans.Where(x => x.Id == id).Select(x => x.PatientId),
            PatientResource.RoutineAssignment => db.RoutineAssignments.Where(x => x.Id == id).Select(x => (Guid?)x.PatientId),
            PatientResource.NutritionAssignment => db.NutritionPlanAssignments.Where(x => x.Id == id).Select(x => (Guid?)x.PatientId),
            PatientResource.Adaptation => db.AdaptationRecommendations.Where(x => x.Id == id).Select(x => (Guid?)x.Enrollment!.PatientId),
            PatientResource.Weakness => db.Weaknesses.Where(x => x.Id == id).Select(x => (Guid?)x.PatientId),
            PatientResource.Intervention => db.Interventions.Where(x => x.Id == id).Select(x => (Guid?)x.PatientId),
            PatientResource.ClinicalReview => db.ClinicalXpReviews.Where(x => x.Id == id).Select(x => (Guid?)x.PatientId),
            PatientResource.Document => db.Documents.Where(x => x.Id == id).Select(x => x.PatientId),
            _ => throw new ArgumentOutOfRangeException(nameof(resource)),
        }).SingleOrDefaultAsync(ct);
}
