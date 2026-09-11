namespace CoppAddresd.Application.Common;

/// <summary>
/// Configuración del envío proactivo de recordatorios por hito del programa
/// (Program Milestone Reminder): días de hito, ventana de entrega local del
/// paciente, días de gracia, reintentos y plantillas estáticas por día. El job
/// <c>ProgramMilestoneSenderJob</c> lee esta sección vía IOptions.
/// </summary>
public class ProgramMilestoneSenderSettings
{
    public const string SectionName = "Program:MilestoneSender";

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
    public List<MilestoneTemplateSettings> Templates { get; set; } = [];
}

/// <summary>
/// Plantilla configurable de un recordatorio de hito: copy neutral en español
/// (título + mensaje) y el agente de chat que recibe la inyección proactiva.
/// </summary>
public sealed class MilestoneTemplateSettings
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
