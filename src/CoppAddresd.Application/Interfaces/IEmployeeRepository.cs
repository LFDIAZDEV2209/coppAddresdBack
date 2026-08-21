using CoppAddresd.Application.Features.Professionals;
using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio del directorio de empleados y su extensión clínica
/// (schema <c>erp</c>). Cubre empleados (núcleo HR), profesionales
/// (extensión 1:0..1) y las asignaciones a clínicas, especialidades y
/// licencias. La implementación EF vive en Infrastructure.
/// </summary>
public interface IEmployeeRepository
{
    Task<(IReadOnlyList<Employee> Items, int Total)> ListAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        Guid? organizationId,
        Guid? clinicId,
        CancellationToken ct = default);

    /// <summary>Empleado con asignaciones de clínicas y extensión profesional completa.</summary>
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Empleado por id de su extensión profesional (<c>erp.professionals</c>),
    /// con clínicas + sedes + extensión. Para los datos de referencia del
    /// microservicio de Telemedicina, que referencia al profesional por su id.
    /// </summary>
    Task<Employee?> GetByProfessionalIdAsync(Guid professionalId, CancellationToken ct = default);

    /// <summary>Empleado por usuario de Auth (contexto del JWT), con clínicas + sedes + extensión.</summary>
    Task<Employee?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Lista paginada de profesionales clínicos (empleados con extensión
    /// <see cref="Professional"/>) con sus especialidades y sedes de atención.
    /// Es el catálogo que consume la UI (elegir profesional al crear una
    /// solicitud de telemedicina, directorio admin). Filtros opcionales:
    /// búsqueda, estado, especialidad, sede, organización y clínica.
    /// </summary>
    Task<(IReadOnlyList<Employee> Items, int Total)> ListProfessionalsAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        Guid? specialtyId,
        Guid? locationId,
        Guid? organizationId,
        Guid? clinicId,
        CancellationToken ct = default);

    Task<bool> EmailExistsInOrganizationAsync(Guid organizationId, string email, Guid? excludeEmployeeId = null, CancellationToken ct = default);

    Task<Employee> AddAsync(Employee employee, CancellationToken ct = default);

    Task UpdateAsync(Employee employee, CancellationToken ct = default);

    /// <summary>Vincula el usuario de Auth al empleado (tras la invitación).</summary>
    Task SetUserIdAsync(Guid employeeId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Elimina físicamente un empleado. Solo como compensación del flujo de
    /// creación orquestado (el empleado recién creado no tiene historial): el
    /// borrado administrativo normal usa cambios de estado.
    /// </summary>
    Task DeleteAsync(Guid employeeId, CancellationToken ct = default);

    /// <summary>
    /// Completa el onboarding del profesional: actualiza la extensión clínica
    /// (tipo, bio, foto, teléfono), reemplaza especialidades y licencias y, si
    /// <paramref name="completeOnboarding"/> es true, marca
    /// <c>OnboardingCompletedAt</c> y activa al empleado. Todo en una transacción.
    /// </summary>
    Task CompleteOnboardingAsync(
        Guid employeeId,
        Guid? professionalTypeId,
        string? bio,
        string? photoStorageKey,
        string? phoneCountryCode,
        string? phoneNumber,
        IReadOnlyList<Guid> specialtyIds,
        IReadOnlyList<LicenseInput> licenses,
        bool completeOnboarding,
        CancellationToken ct = default);

    Task<Professional> AddProfessionalAsync(Professional professional, CancellationToken ct = default);

    Task UpdateProfessionalAsync(Professional professional, CancellationToken ct = default);

    Task<bool> ClinicExistsAsync(Guid clinicId, CancellationToken ct = default);

    Task<bool> SpecialtyExistsAsync(Guid specialtyId, CancellationToken ct = default);
}
