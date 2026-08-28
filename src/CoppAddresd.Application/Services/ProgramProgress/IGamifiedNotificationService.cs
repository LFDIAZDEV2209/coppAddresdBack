namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Servicio de notificaciones gamificadas del programa (SPEC §20, "Paso 7b"):
/// inserta el log en <c>app.notifications</c> y, en segundo plano best-effort,
/// envía el push FCM por el camino existente del módulo de notificaciones
/// (tokens de <c>app.device_tokens</c> + <see cref="Interfaces.IFcmClient"/>).
///
/// Semántica (SPEC §20, B):
/// - <b>Best-effort</b>: NUNCA lanza. Un fallo de envío o de persistencia se
///   registra y se retorna; jamás rompe la transacción de otorgamiento de XP
///   que lo invoca (AC-42).
/// - <b>Anti-spam</b>: máx. 2 por tipo por día local, máx. 6 totales por día
///   local (config <c>Program:Notifications</c>); límite alcanzado → se omite
///   en silencio (solo log debug).
/// - <b>Horario de silencio</b>: 22:00–07:00 local del paciente; la prioridad
///   <c>critical</c> lo ignora.
/// </summary>
public interface IGamifiedNotificationService
{
    /// <summary>
    /// Genera una notificación gamificada para el paciente (log + push
    /// best-effort). Los tipos canónicos (SPEC §20, C) son
    /// <c>milestone_reached</c>, <c>nb_milestone</c>, <c>level_up</c> y
    /// <c>day_complete</c>; la prioridad es <c>normal</c>, <c>high</c> o
    /// <c>critical</c>. Solo se invoca en flujos de otorgamiento, tras la
    /// escritura de la XP y en la primera concesión (nunca en replay).
    /// </summary>
    Task NotifyAsync(
        Guid patientId,
        string type,
        string title,
        string message,
        string priority,
        CancellationToken ct = default);
}