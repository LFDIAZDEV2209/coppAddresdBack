using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio de la estructura organizacional del ERP (schema <c>erp</c>):
/// organizaciones, clínicas, sedes y los catálogos de profesiones y
/// especialidades. La implementación EF vive en Infrastructure.
/// </summary>
public interface IOrganizationRepository
{
    Task<IReadOnlyList<Organization>> ListOrganizationsAsync(CancellationToken ct = default);

    /// <summary>Árbol completo organización → clínicas → sedes (para navegación y contexto).</summary>
    Task<IReadOnlyList<Organization>> ListTreeAsync(CancellationToken ct = default);

    Task<Organization?> GetOrganizationByIdAsync(Guid id, CancellationToken ct = default);

    Task<Organization> AddOrganizationAsync(
        Organization organization,
        CancellationToken ct = default
    );

    Task UpdateOrganizationAsync(Organization organization, CancellationToken ct = default);

    Task<Clinic?> GetClinicByIdAsync(Guid id, CancellationToken ct = default);

    Task<Clinic> AddClinicAsync(Clinic clinic, CancellationToken ct = default);

    Task UpdateClinicAsync(Clinic clinic, CancellationToken ct = default);

    Task<Location?> GetLocationByIdAsync(Guid id, CancellationToken ct = default);

    Task<Location> AddLocationAsync(Location location, CancellationToken ct = default);

    Task UpdateLocationAsync(Location location, CancellationToken ct = default);

    Task<IReadOnlyList<ProfessionalType>> ListProfessionalTypesAsync(
        CancellationToken ct = default
    );

    Task<IReadOnlyList<Specialty>> ListSpecialtiesAsync(CancellationToken ct = default);

    /// <summary>Especialidades válidas para una profesión (mapeo N:N del catálogo).</summary>
    Task<IReadOnlyList<Specialty>> ListSpecialtiesByProfessionalTypeAsync(
        Guid professionalTypeId,
        CancellationToken ct = default
    );

    Task<ProfessionalType?> GetProfessionalTypeByIdAsync(Guid id, CancellationToken ct = default);

    Task<Specialty?> GetSpecialtyByIdAsync(Guid id, CancellationToken ct = default);

    Task<bool> ProfessionalTypeCodeExistsAsync(string code, CancellationToken ct = default);

    Task<bool> SpecialtyCodeExistsAsync(string code, CancellationToken ct = default);

    Task<ProfessionalType> AddProfessionalTypeAsync(
        ProfessionalType type,
        CancellationToken ct = default
    );

    Task UpdateProfessionalTypeAsync(ProfessionalType type, CancellationToken ct = default);

    Task<Specialty> AddSpecialtyAsync(Specialty specialty, CancellationToken ct = default);

    Task UpdateSpecialtyAsync(Specialty specialty, CancellationToken ct = default);
}
