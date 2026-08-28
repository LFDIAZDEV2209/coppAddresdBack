using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Features.Professionals;

/// <summary>Asignación del empleado a una clínica.</summary>
public record EmployeeClinicDto(Guid ClinicId, string ClinicName, bool IsPrimary, string Status)
{
    public static EmployeeClinicDto FromEntity(EmployeeClinic entity) =>
        new(entity.ClinicId, entity.Clinic.Name, entity.IsPrimary, entity.Status);
}

/// <summary>Credencial del profesional.</summary>
public record ProfessionalLicenseDto(
    Guid Id,
    string LicenseType,
    Guid? SpecialtyId,
    string? SpecialtyName,
    string? Number,
    Guid? StateId,
    string? Issuer,
    DateOnly? IssuedAt,
    DateOnly? ExpiresAt,
    string VerificationStatus
)
{
    public static ProfessionalLicenseDto FromEntity(ProfessionalLicense entity) =>
        new(
            entity.Id,
            entity.LicenseType,
            entity.SpecialtyId,
            entity.Specialty?.Name,
            entity.Number,
            entity.StateId,
            entity.Issuer,
            entity.IssuedAt,
            entity.ExpiresAt,
            entity.VerificationStatus
        );
}

/// <summary>Extensión clínica del empleado (solo existe si es profesional).</summary>
public record ProfessionalDto(
    Guid Id,
    Guid? ProfessionalTypeId,
    string? ProfessionalTypeName,
    string? Bio,
    string? PhotoStorageKey,
    DateTime? OnboardingCompletedAt,
    IReadOnlyList<Guid> SpecialtyIds,
    IReadOnlyList<ProfessionalLicenseDto> Licenses
)
{
    public static ProfessionalDto FromEntity(Professional entity) =>
        new(
            entity.Id,
            entity.ProfessionalTypeId,
            entity.ProfessionalType?.Name,
            entity.Bio,
            entity.PhotoStorageKey,
            entity.OnboardingCompletedAt,
            entity.Specialties.Select(s => s.SpecialtyId).Order().ToList(),
            entity
                .Licenses.OrderBy(l => l.LicenseType)
                .Select(ProfessionalLicenseDto.FromEntity)
                .ToList()
        );
}

/// <summary>Empleado del directorio (fila de la lista).</summary>
public record EmployeeListItemDto(
    Guid Id,
    string FirstName,
    string? MiddleName,
    string LastName,
    string Email,
    string? JobTitle,
    string? Department,
    string Status,
    bool IsProfessional,
    string? ProfessionalTypeName,
    IReadOnlyList<string> SpecialtyNames,
    IReadOnlyList<string> ClinicNames
)
{
    public static EmployeeListItemDto FromEntity(Employee entity) =>
        new(
            entity.Id,
            entity.FirstName,
            entity.MiddleName,
            entity.LastName,
            entity.Email,
            entity.JobTitle,
            entity.Department,
            entity.Status,
            entity.Professional is not null,
            entity.Professional?.ProfessionalType?.Name,
            entity
                .Professional?.Specialties.OrderBy(s => s.Specialty.Name)
                .Select(s => s.Specialty.Name)
                .ToList()
                ?? [],
            entity
                .ClinicAssignments.Where(a => a.Status == "Active")
                .Select(a => a.Clinic.Name)
                .Order()
                .ToList()
        );
}

/// <summary>Empleado completo (detalle) con clínicas y extensión profesional.</summary>
public record EmployeeDto(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    Guid? UserId,
    string FirstName,
    string? MiddleName,
    string LastName,
    string Email,
    string? PhoneCountryCode,
    string? PhoneNumber,
    string? JobTitle,
    string? Department,
    DateOnly? HireDate,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<EmployeeClinicDto> Clinics,
    ProfessionalDto? Professional
)
{
    public static EmployeeDto FromEntity(Employee entity) =>
        new(
            entity.Id,
            entity.OrganizationId,
            entity.Organization.Name,
            entity.UserId,
            entity.FirstName,
            entity.MiddleName,
            entity.LastName,
            entity.Email,
            entity.PhoneCountryCode,
            entity.PhoneNumber,
            entity.JobTitle,
            entity.Department,
            entity.HireDate,
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity
                .ClinicAssignments.OrderByDescending(a => a.IsPrimary)
                .Select(EmployeeClinicDto.FromEntity)
                .ToList(),
            entity.Professional is null ? null : ProfessionalDto.FromEntity(entity.Professional)
        );
}
