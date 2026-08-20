namespace CoppAddresd.Telemedicine.Application.ReferenceData;

/// <summary>
/// DTOs de datos de referencia del ERP que el microservicio de Telemedicina
/// consume vía internal endpoints del backend. El microservicio NO posee estos
/// datos: solo los referencia por Id (referencias débiles). Cada DTO trae lo
/// mínimo para validar existencia y poblar la UI sin PHI innecesaria.
/// </summary>

/// <summary>Profesional (id = <c>erp.professionals</c>).</summary>
public sealed record ProfessionalRefDto(
    Guid Id,
    Guid EmployeeId,
    Guid? UserId,
    string FullName,
    string? ProfessionalTypeName,
    IReadOnlyList<Guid> SpecialtyIds,
    IReadOnlyList<Guid> LocationIds,
    IReadOnlyList<Guid> ClinicIds);

/// <summary>Paciente (id = <c>app.patient_profiles</c>).</summary>
public sealed record PatientRefDto(
    Guid Id,
    string FullName,
    string? Email,
    Guid? ClinicId,
    Guid? LocationId);

/// <summary>Especialidad (id = <c>erp.specialties</c>).</summary>
public sealed record SpecialtyRefDto(
    Guid Id,
    string Code,
    string Name,
    string Category);

/// <summary>Sede (id = <c>erp.locations</c>).</summary>
public sealed record LocationRefDto(
    Guid Id,
    string Name,
    Guid ClinicId,
    bool IsActive);
