namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Credencial o licencia de un <see cref="Professional"/> (State License, Board
/// Certification, DEA, NPI...). Separada de la profesión y de la especialidad
/// por diseño: un profesional puede tener N credenciales con vigencias y
/// estados de verificación propios. El tipo de credencial es un vocabulario
/// cerrado (códigos estables en inglés); la especialidad asociada es opcional
/// (ej. board certification de Obesity Medicine).
/// </summary>
public sealed class ProfessionalLicense
{
    public Guid Id { get; set; }

    public Guid ProfessionalId { get; set; }

    /// <summary>Tipo de credencial (vocabulario cerrado: StateLicense, BoardCertification, DeaRegistration, Npi, CdrLicense, NbcHwcCertification, BlsAcls, Other).</summary>
    public string LicenseType { get; set; } = default!;

    /// <summary>Especialidad certificada (opcional, ej. board certification).</summary>
    public Guid? SpecialtyId { get; set; }

    /// <summary>Número de licencia/NPI/DEA.</summary>
    public string? Number { get; set; }

    /// <summary>Estado emisor (catálogo geográfico del schema app).</summary>
    public Guid? StateId { get; set; }

    /// <summary>Entidad emisora (ej. "Texas Medical Board", "ABOM").</summary>
    public string? Issuer { get; set; }

    public DateOnly? IssuedAt { get; set; }

    public DateOnly? ExpiresAt { get; set; }

    /// <summary>Estado de verificación (Pending, Verified, Expired, Revoked).</summary>
    public string VerificationStatus { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Professional Professional { get; set; } = default!;

    public Specialty? Specialty { get; set; }
}
