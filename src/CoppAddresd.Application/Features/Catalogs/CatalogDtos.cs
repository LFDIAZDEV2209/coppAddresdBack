namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>País del catálogo geográfico.</summary>
public record CountryDto(
    Guid Id,
    string Code,
    string Name,
    string PhoneCode,
    bool IsActive,
    int SortOrder);

/// <summary>Estado/provincia del catálogo geográfico.</summary>
public record StateDto(Guid Id, string Code, string Name);

/// <summary>Ciudad del catálogo geográfico.</summary>
public record CityDto(Guid Id, string Name);

/// <summary>Código postal del catálogo geográfico (US ZIP).</summary>
public record PostalCodeDto(Guid Id, string ZipCode);

/// <summary>Opción de un catálogo cerrado (grupo sanguíneo, tipo de documento, etnia).</summary>
public record CatalogOptionDto(Guid Id, string Code, string Name, int SortOrder);

/// <summary>Resultado de búsqueda en un catálogo clínico (ICD-10, medicamentos, alergenos).</summary>
public record CatalogSearchItemDto(Guid Id, string? Code, string Name, string? Description);