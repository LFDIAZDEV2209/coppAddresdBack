using CoppAddresd.Telemedicine.Application.ReferenceData;

namespace CoppAddresd.Telemedicine.Application.Interfaces;

/// <summary>
/// Acceso a los datos de referencia del ERP (profesionales, pacientes,
/// especialidades, sedes). El dominio de telemedicina referencia estos datos
/// por Id; su validación de existencia y los datos de la UI vienen del backend
/// vía internal endpoints (<c>X-Internal-Key</c>). Implementación en
/// Infrastructure (<see cref="CoppAddresd.Telemedicine.Infrastructure.Services.AppointmentReferenceDataService"/>),
/// sustituible en pruebas por un fake.
/// </summary>
public interface IAppointmentReferenceDataService
{
    /// <summary>Profesional por su id de <c>erp.professionals</c>; <c>null</c> si no existe.</summary>
    Task<ProfessionalRefDto?> GetProfessionalAsync(Guid professionalId, CancellationToken ct = default);

    /// <summary>Paciente por su id de <c>app.patient_profiles</c>; <c>null</c> si no existe.</summary>
    Task<PatientRefDto?> GetPatientAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>Profesional del usuario de Auth (contexto del JWT); <c>null</c> si el usuario no es profesional.</summary>
    Task<ProfessionalRefDto?> GetProfessionalByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Paciente del usuario de Auth (contexto del JWT); <c>null</c> si el usuario no es paciente.</summary>
    Task<PatientRefDto?> GetPatientByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Especialidad por id de <c>erp.specialties</c>; <c>null</c> si no existe.</summary>
    Task<SpecialtyRefDto?> GetSpecialtyAsync(Guid specialtyId, CancellationToken ct = default);

    /// <summary>Sede por id de <c>erp.locations</c>; <c>null</c> si no existe.</summary>
    Task<LocationRefDto?> GetLocationAsync(Guid locationId, CancellationToken ct = default);
}
