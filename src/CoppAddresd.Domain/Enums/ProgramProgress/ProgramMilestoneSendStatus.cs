namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Estado de un envío de recordatorio de hito del programa
/// (<c>program_milestone_sends</c>). Pending = pendiente de envío (o reintento
/// programado tras un fallo transitorio), Sent = enviado al menos una vez
/// (terminal), Failed = agotó los reintentos (terminal), Skipped = se omitió
/// por configuración (plantilla inexistente, terminal).
/// </summary>
public enum ProgramMilestoneSendStatus
{
    /// <summary>Pendiente: fila reclamada para envío o fallo transitorio reintentable.</summary>
    Pending = 1,

    /// <summary>Enviado: push + mensaje proactivo de chat confirmados.</summary>
    Sent = 2,

    /// <summary>Falló de forma permanente: se agotaron los reintentos.</summary>
    Failed = 3,

    /// <summary>Omitido: plantilla no configurada para el día del hito.</summary>
    Skipped = 4,
}
