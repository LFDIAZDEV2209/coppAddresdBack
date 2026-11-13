using System.Linq.Expressions;

namespace CoppAddresd.Application.Interfaces;

/// <summary>Identidad y capacidades del actor; nunca se obtienen de parámetros del cliente.</summary>
public interface IPatientScopeActor
{
    Guid? UserId { get; }
    bool IsAdmin { get; }
    Task<Guid?> ProfessionalIdAsync(CancellationToken ct);
    Task<bool> HasPermissionAsync(string permission, CancellationToken ct);
}

public enum PatientResource { Enrollment, Week, Prescription, Cycle, Intake, FoodAnalysis,
    NutritionPlan, RoutineAssignment, NutritionAssignment, Adaptation, Weakness, Intervention, ClinicalReview, Document }

/// <summary>Persistencia del alcance; la consulta permanece diferida hasta contar o paginar.</summary>
public interface IPatientScopeStore
{
    IQueryable<Guid> VisiblePatients(Guid? professionalId, bool isAdmin);
    Task<bool> ContainsAsync(IQueryable<Guid> patientIds, Guid patientId, CancellationToken ct);
    Task<Guid?> OwnerAsync(PatientResource resource, Guid id, CancellationToken ct);
}

/// <summary>Permiso Y paciente asignado. Admin solo exceptúa la asignación, nunca el permiso.</summary>
public interface IPatientScopeGuard
{
    Task EnsureAsync(Guid patientId, CancellationToken ct);
    Task EnsureAsync(Guid patientId, string permission, CancellationToken ct);
    Task<Guid> EnsureResourceAsync(PatientResource resource, Guid id, string permission, CancellationToken ct);
    Task<IQueryable<T>> FilterAsync<T>(IQueryable<T> query, Expression<Func<T, Guid>> patientId,
        string permission, CancellationToken ct);
}
