namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>Contenido de un control de hito listo para enviar.</summary>
public sealed record ProgramControlTemplate(
    string Title,
    string Message,
    string AgentTypeId);

/// <summary>
/// Proveedor de plantillas de control por día de hito. Abstracción de
/// contenido: la v1 entrega texto predefinido por configuración
/// (<see cref="PredefinedProgramControlTemplateProvider"/>); una versión futura
/// podrá generar el contenido con LLM implementando esta misma interfaz, sin
/// tocar el scheduler (<c>ProgramControlJob</c>).
/// </summary>
public interface IProgramControlTemplateProvider
{
    /// <summary>
    /// Devuelve la plantilla para el día de hito indicado, o null si el día no
    /// tiene contenido configurado (el job marca el control como Skipped).
    /// </summary>
    Task<ProgramControlTemplate?> GetAsync(int milestoneDay, CancellationToken ct = default);
}