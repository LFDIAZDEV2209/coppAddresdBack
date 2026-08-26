using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Features.Professionals;

/// <summary>Organización del directorio del ERP.</summary>
public record OrganizationDto(Guid Id, string Code, string Name, bool IsActive)
{
    public static OrganizationDto FromEntity(Organization entity) =>
        new(entity.Id, entity.Code, entity.Name, entity.IsActive);
}

/// <summary>Clínica de una organización.</summary>
public record ClinicDto(Guid Id, Guid OrganizationId, string Name, string? Code, bool IsActive)
{
    public static ClinicDto FromEntity(Clinic entity) =>
        new(entity.Id, entity.OrganizationId, entity.Name, entity.Code, entity.IsActive);
}

/// <summary>Sede física de una clínica.</summary>
public record LocationDto(
    Guid Id,
    Guid ClinicId,
    string Name,
    string? AddressLine1,
    string? AddressLine2,
    Guid? CityId,
    Guid? StateId,
    string? PostalCode,
    string? PhoneCountryCode,
    string? PhoneNumber,
    bool IsActive
)
{
    public static LocationDto FromEntity(Location entity) =>
        new(
            entity.Id,
            entity.ClinicId,
            entity.Name,
            entity.AddressLine1,
            entity.AddressLine2,
            entity.CityId,
            entity.StateId,
            entity.PostalCode,
            entity.PhoneCountryCode,
            entity.PhoneNumber,
            entity.IsActive
        );
}

/// <summary>Nodo del árbol organizacional (organización → clínicas → sedes).</summary>
public record OrganizationTreeNodeDto(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    IReadOnlyList<ClinicTreeNodeDto> Clinics
)
{
    public static OrganizationTreeNodeDto FromEntity(Organization entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.IsActive,
            entity.Clinics.OrderBy(c => c.Name).Select(ClinicTreeNodeDto.FromEntity).ToList()
        );
}

/// <summary>Nodo clínica del árbol (con sus sedes).</summary>
public record ClinicTreeNodeDto(
    Guid Id,
    string Name,
    string? Code,
    bool IsActive,
    IReadOnlyList<LocationTreeNodeDto> Locations
)
{
    public static ClinicTreeNodeDto FromEntity(Clinic entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.Code,
            entity.IsActive,
            entity.Locations.OrderBy(l => l.Name).Select(LocationTreeNodeDto.FromEntity).ToList()
        );
}

/// <summary>Nodo sede del árbol.</summary>
public record LocationTreeNodeDto(
    Guid Id,
    string Name,
    string? CityName,
    string? StateCode,
    bool IsActive
)
{
    public static LocationTreeNodeDto FromEntity(Location entity) =>
        new(entity.Id, entity.Name, entity.City?.Name, entity.State?.Code, entity.IsActive);
}

/// <summary>Profesión del catálogo (professional_types).</summary>
public record ProfessionalTypeDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<Guid> ValidSpecialtyIds
)
{
    public static ProfessionalTypeDto FromEntity(ProfessionalType entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Description,
            entity.IsActive,
            entity.Specialties.Select(x => x.SpecialtyId).Order().ToList()
        );
}

/// <summary>Especialidad del catálogo, agrupada por categoría.</summary>
public record SpecialtyDto(
    Guid Id,
    string Code,
    string Name,
    string Category,
    string? Description,
    bool IsActive
)
{
    public static SpecialtyDto FromEntity(Specialty entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Category,
            entity.Description,
            entity.IsActive
        );
}
