namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Sede física de una <see cref="Clinic"/>. Nivel inferior del scope de
/// autorización: un profesional puede atender en sedes concretas de una
/// clínica y un administrador puede administrar profesionales de una sede
/// específica. La geografía referencia los catálogos del schema <c>app</c>.
/// </summary>
public sealed class Location
{
    public Guid Id { get; set; }

    public Guid ClinicId { get; set; }

    public string Name { get; set; } = default!;

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public Guid? CityId { get; set; }

    public Guid? StateId { get; set; }

    public string? PostalCode { get; set; }

    /// <summary>Código telefónico E.164 sin '+' (ej. "1" para USA).</summary>
    public string? PhoneCountryCode { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Clinic Clinic { get; set; } = default!;

    public City? City { get; set; }

    public State? State { get; set; }

    public ICollection<ProfessionalLocation> ProfessionalLocations { get; set; } = [];
}
