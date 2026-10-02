namespace CoppAddresd.Auth.Interfaces;

/// <summary>
/// Guardia de acceso a la app móvil para pacientes. Consulta de solo lectura
/// <c>app.patient_profiles</c> por <c>user_id</c> y reporta si el perfil está
/// bloqueado (estado exactamente <c>"Inactivo"</c>). Los usuarios sin perfil
/// de paciente (staff/ERP) nunca se bloquean.
/// </summary>
public interface IPatientAccessGuard
{
    /// <summary>
    /// Indica si el usuario está vinculado a un perfil de paciente inactivo.
    /// </summary>
    Task<bool> IsBlockedAsync(Guid userId, CancellationToken ct = default);
}
