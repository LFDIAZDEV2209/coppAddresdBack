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

    /// <summary>Empleado por usuario de Auth (contexto del JWT), con clínicas + sedes + extensión.</summary>
    Task<Employee?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task<bool> EmailExistsInOrganizationAsync(Guid organizationId, string email, Guid? excludeEmployeeId = null, CancellationToken ct = default);

    Task<Employee> AddAsync(Employee employee, CancellationToken ct = default);

    Task UpdateAsync(Employee employee, CancellationToken ct = default);

    Task<Professional> AddProfessionalAsync(Professional professional, CancellationToken ct = default);

    Task UpdateProfessionalAsync(Professional professional, CancellationToken ct = default);

    Task<bool> ClinicExistsAsync(Guid clinicId, CancellationToken ct = default);

    Task<bool> SpecialtyExistsAsync(Guid specialtyId, CancellationToken ct = default);
}
