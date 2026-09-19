namespace CoppAddresd.Telemedicine.Application.Constants;

/// <summary>
/// Contrato de formato de las métricas pre-agregadas de analytics
/// (<c>tele.appointment_daily_metrics</c>). Los tres escritores/lectores deben
/// usar estas claves: el processor en segundo plano
/// (<c>TelemedicineMetricsProcessorHostedService</c>), el backfill
/// (<c>IMetricsBackfillService</c>) y las lecturas del dashboard
/// (<c>AppointmentRepository</c>). Cambiar una clave exige backfill total.
/// </summary>
public static class TelemedicineMetricKeys
{
    /// <summary>Conteo total de citas del día (dimensión <see cref="GeneralDimension"/>).</summary>
    public const string DailyTotal = "daily_total";

    /// <summary>Conteo por estado; la dimensión es el nombre del enum <c>AppointmentStatus</c>.</summary>
    public const string StatusCount = "status_count";

    /// <summary>Conteo por hora; la dimensión es <c>Hour_HH</c> (hora UTC, 24h).</summary>
    public const string HourlyCount = "hourly_count";

    /// <summary>
    /// Salas abiertas por primera vez en la cita (join-token o session/start;
    /// una reapertura NO vuelve a contarla). Dimensión
    /// <see cref="GeneralDimension"/>.
    /// </summary>
    public const string RoomsOpened = "rooms_opened";

    /// <summary>Sesiones de video iniciadas (dimensión <see cref="GeneralDimension"/>).</summary>
    public const string SessionsStarted = "sessions_started";

    /// <summary>
    /// Sesiones de video terminadas (fin manual, webhook <c>room-ended</c> o
    /// barrido); dimensión <see cref="GeneralDimension"/>.
    /// </summary>
    public const string SessionsEnded = "sessions_ended";

    /// <summary>
    /// Suma de segundos de sesiones terminadas (base del promedio:
    /// <c>suma / sessions_ended</c>); dimensión <see cref="GeneralDimension"/>.
    /// </summary>
    public const string SessionDurationSeconds = "session_duration_seconds";

    /// <summary>
    /// Reaperturas de consultas completadas (transición
    /// <c>Completed → InProgress</c>); dimensión <see cref="GeneralDimension"/>.
    /// </summary>
    public const string Reopens = "reopens";

    /// <summary>
    /// Mensajes de chat persistidos; la dimensión es el rol del emisor
    /// (<see cref="ProfessionalDimension"/>, <see cref="PatientDimension"/> o
    /// <see cref="SupervisorDimension"/>) derivado del JWT.
    /// </summary>
    public const string ChatMessagesSent = "chat_messages_sent";

    /// <summary>
    /// P2 — tokens de sala emitidos. Fuera del set v1 (no se emite ni se
    /// reconstruye); el lector la expone en 0 si no hay filas.
    /// </summary>
    public const string JoinTokensIssued = "join_tokens_issued";

    /// <summary>
    /// P2 — conexiones de participante reportadas por el proveedor. Fuera del
    /// set v1 (no se emite ni se reconstruye); el lector la expone en 0.
    /// </summary>
    public const string ParticipantConnections = "participant_connections";

    /// <summary>Dimensión del conteo total del día.</summary>
    public const string GeneralDimension = "general";

    /// <summary>Dimensión de rol del emisor profesional.</summary>
    public const string ProfessionalDimension = "Professional";

    /// <summary>Dimensión de rol del emisor paciente.</summary>
    public const string PatientDimension = "Patient";

    /// <summary>Dimensión de rol del emisor supervisor.</summary>
    public const string SupervisorDimension = "Supervisor";

    /// <summary>Dimensión de rol no resoluble (P2 participant_connections).</summary>
    public const string UnknownDimension = "Unknown";

    /// <summary>Prefijo de la dimensión horaria (<c>Hour_00</c> … <c>Hour_23</c>).</summary>
    public const string HourDimensionPrefix = "Hour_";

    /// <summary>
    /// Fila espejo global (todas las clínicas/profesionales). Mismo convenio que
    /// el processor: <c>Guid.Empty</c>.
    /// </summary>
    public static readonly Guid GlobalProfessionalId = Guid.Empty;
}
