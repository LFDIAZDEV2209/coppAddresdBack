namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Estado de un control conversacional del programa
/// (<c>program_controls</c>). Pending = pendiente de envío (o reintento
/// programado tras un fallo transitorio), Sent = enviado al menos una vez (ya
/// NO es terminal: puede avanzar a Responded/Completed/FollowedUp/Missed según
/// la interacción del paciente), Failed = agotó los reintentos (terminal),
/// Skipped = se omitió por configuración (plantilla inexistente, terminal).
/// Los estados de la fase 2 extienden la máquina sin tocar la columna status
/// de la fase 1.
/// </summary>
public enum ProgramControlStatus
{
    /// <summary>Pendiente: fila reclamada para envío o fallo transitorio reintentable.</summary>
    Pending = 1,

    /// <summary>Enviado: push + mensaje proactivo de chat confirmados (no terminal).</summary>
    Sent = 2,

    /// <summary>Falló de forma permanente: se agotaron los reintentos.</summary>
    Failed = 3,

    /// <summary>Omitido: plantilla no configurada para el día del hito.</summary>
    Skipped = 4,

    /// <summary>Respondido: el paciente escribió en el control abierto (cualquier mensaje).</summary>
    Responded = 5,

    /// <summary>Completado: subió el examen de laboratorio asociado (o cierre positivo).</summary>
    Completed = 6,

    /// <summary>Cerrado sin examen: rechazo explícito o vencimiento de la ventana de subida.</summary>
    ClosedWithoutExam = 7,

    /// <summary>Seguimiento enviado: follow-up de la fase 2 (tras FollowupHours).</summary>
    FollowedUp = 8,

    /// <summary>Perdido: sin respuesta tras el follow-up (MissedAfterFollowupHours).</summary>
    Missed = 9,
}