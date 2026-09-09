namespace CoppAddresd.Application.DTOs.ProgramProgress;

/// <summary>
/// Información resumida del paciente para la emisión de eventos de biometría.
/// </summary>
public sealed record PatientBiometriaInfoDto(
    Guid PatientId,
    string? Gender,
    Guid? CityId
);
