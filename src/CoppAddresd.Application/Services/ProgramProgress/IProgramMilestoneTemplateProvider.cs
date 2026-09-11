namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>Contenido de un recordatorio de hito listo para enviar.</summary>
public sealed record MilestoneTemplate(
    string Title,
    string Message,
    string AgentTypeId);

/// <summary>
/// Proveedor de plantillas de recordatorio por día de hito. Abstracción de
/// contenido: la v1 entrega texto predefinido por configuración
/// (<see cref="PredefinedMilestoneTemplateProvider"/>); una versión futura
/// podrá generar el contenido con LLM implementando esta misma interfaz, sin
/// tocar el scheduler (<c>ProgramMilestoneSenderJob</c>).
/// </summary>
public interface IProgramMilestoneTemplateProvider
{
    /// <summary>
    /// Devuelve la plantilla para el día de hito indicado, o null si el día no
    /// tiene contenido configurado (el job marca el envío como Skipped).
    /// </summary>
    Task<MilestoneTemplate?> GetAsync(int milestoneDay, CancellationToken ct = default);
}
