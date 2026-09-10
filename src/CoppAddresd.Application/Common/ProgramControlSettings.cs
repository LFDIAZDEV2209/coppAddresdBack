namespace CoppAddresd.Application.Common;

/// <summary>
/// Configuración del envío proactivo de recordatorios por hito del programa
/// (Controles, <c>Program:Controls</c>): días de hito, ventana de entrega local
/// del paciente, días de gracia, reintentos y plantillas estáticas por día, más
/// el killswitch del flujo conversacional y los plazos de la fase 2
/// (follow-up, cierre sin examen). El job <c>ProgramControlJob</c> lee esta
/// sección vía IOptions.
/// </summary>
public class ProgramControlSettings
{
    public const string SectionName = "Program:Controls";

    /// <summary>Habilita el job periódico (default true).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Minutos entre pasadas del job (default 60).</summary>
    public int TickMinutes { get; set; } = 60;

    /// <summary>Hora local inicial de la ventana de entrega (default 9).</summary>
    public int StartLocalHour { get; set; } = 9;

    /// <summary>Hora local final de la ventana de entrega, exclusiva (default 21).</summary>
    public int EndLocalHour { get; set; } = 21;

    /// <summary>
    /// Días de gracia tras el día exacto del hito en los que el recordatorio
    /// sigue considerándose "a tiempo" (default 3).
    /// </summary>
    public int GraceDays { get; set; } = 3;

    /// <summary>Máximo de intentos de notificación antes de marcar Failed (default 3).</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Días del programa en los que se envía recordatorio.</summary>
    public List<int> Days { get; set; } = [7, 14, 21, 45, 60, 90];

    /// <summary>Plantillas estáticas (v1) por día de hito.</summary>
    public List<ProgramControlTemplateSettings> Templates { get; set; } = [];

    /// <summary>
    /// Killswitch del flujo conversacional de controles (fase 2): con false
    /// (default) el backend se comporta como UC-001 puro — la columna status
    /// solo avanza por la fase 1 y los hooks de chat/exámenes no se enganchan.
    /// </summary>
    public bool ControlsEnabled { get; set; } = false;

    /// <summary>Horas desde Responded tras las cuales se envía el follow-up (default 48).</summary>
    public int FollowupHours { get; set; } = 48;

    /// <summary>Horas desde el follow-up tras las cuales el control pasa a Missed (default 48).</summary>
    public int MissedAfterFollowupHours { get; set; } = 48;

    /// <summary>Días desde Responded sin subir examen tras los cuales se cierra sin examen (default 7).</summary>
    public int NoUploadCloseDays { get; set; } = 7;
}

/// <summary>
/// Plantilla configurable de un recordatorio de hito: copy neutral en español
/// (título + mensaje) y el agente de chat que recibe la inyección proactiva.
/// </summary>
public sealed class ProgramControlTemplateSettings
{
    /// <summary>Día del programa del hito al que aplica esta plantilla.</summary>
    public int Day { get; set; }

    /// <summary>Título del push (copy neutral, cálido, con emoji).</summary>
    public string Title { get; set; } = "";

    /// <summary>Cuerpo del mensaje (push + inyección proactiva en el chat).</summary>
    public string Message { get; set; } = "";

    /// <summary>Agente de chat destino (default "base").</summary>
    public string AgentTypeId { get; set; } = "base";
}