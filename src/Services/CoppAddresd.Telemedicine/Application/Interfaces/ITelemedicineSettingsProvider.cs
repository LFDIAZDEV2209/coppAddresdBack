namespace CoppAddresd.Telemedicine.Application.Interfaces;

/// <summary>
/// Resuelve la configuración operativa de telemedicina para un contexto
/// (organización/clínica). Implementación en Infrastructure: consulta
/// <c>tele.telemedicine_settings</c> con fallback a la configuración global de
/// la organización. Fuente única de las reglas parametrizadas del módulo.
/// </summary>
public interface ITelemedicineSettingsProvider
{
    /// <summary>Configuración efectiva para la clínica (o la global si no hay fila).</summary>
    Task<Domain.Entities.TelemedicineSettings> GetSettingsAsync(
        Guid organizationId,
        Guid? clinicId,
        CancellationToken ct = default);
}
