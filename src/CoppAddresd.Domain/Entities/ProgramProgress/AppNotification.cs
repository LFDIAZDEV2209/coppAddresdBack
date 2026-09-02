namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Log de notificaciones gamificadas del programa (SPEC §20, "Paso 7b"): una
/// fila por notificación generada en los flujos de otorgamiento del módulo
/// (hito de racha, hito de la racha del nutracéutico, subida de nivel y día
/// perfecto). El registro es la fuente de verdad del centro de notificaciones
/// del móvil (<c>GET /api/v1/program/notifications</c>) y la base del
/// anti-spam (máx. por tipo/día, máx. total por día y horario de silencio,
/// SPEC §20, B). El envío push es best-effort: el log nunca depende de que el
/// envío FCM tenga éxito (AC-42) y un fallo del servicio jamás rompe la
/// transacción de otorgamiento de XP.
/// </summary>
public sealed class AppNotification
{
    public Guid Id { get; set; }

    /// <summary>Paciente dueño de la notificación (<c>app.patient_profiles.id</c>).</summary>
    public Guid PatientId { get; set; }

    /// <summary>
    /// Código del evento que generó la notificación (SPEC §20, C):
    /// <c>milestone_reached</c> (hito de racha 7/11/22/50),
    /// <c>nb_milestone</c> (hito de la racha del nutracéutico),
    /// <c>level_up</c> (cruce de umbral de nivel) o <c>day_complete</c>
    /// (día perfecto). Usado por el anti-spam (máx. 2 por tipo por día).
    /// </summary>
    public string Type { get; set; } = default!;

    /// <summary>Título corto de la notificación (varchar 120).</summary>
    public string Title { get; set; } = default!;

    /// <summary>Mensaje completo de la notificación (texto libre, copy de gamificación).</summary>
    public string Message { get; set; } = default!;

    /// <summary>
    /// Prioridad de la notificación (SPEC §20, B): <c>normal</c> / <c>high</c> /
    /// <c>critical</c>. La crítica ignora el horario de silencio; las demás se
    /// omiten en la ventana 22:00–07:00 local del paciente.
    /// </summary>
    public string Priority { get; set; } = "normal";

    /// <summary>Canal de entrega: <c>push</c> (único soportado en MVP).</summary>
    public string Channel { get; set; } = "push";

    /// <summary>Instante (reloj del servidor, UTC) en que se generó la notificación.</summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>Instante en que el paciente la marcó como leída; null = pendiente.</summary>
    public DateTime? ReadAt { get; set; }

    public PatientProfile? Patient { get; set; }
}