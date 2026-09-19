using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Application.Interfaces;

/// <summary>
/// Persistencia del agregado cita de telemedicina y su historial
/// (cancelaciones/reprogramaciones). La integridad del calendario se protege
/// en dos capas: verificación de solapamiento en aplicación (mensaje de error
/// amigable) y constraint en base de datos (índice único parcial + exclusión
/// de solapamiento) como garantía real ante concurrencia.
/// </summary>
public interface IAppointmentRepository
{
    /// <summary>Cita por id, lectura sin tracking (solo lectura).</summary>
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Cita por id TRACKEADA con su historial (cancelaciones/reprogramaciones),
    /// sala virtual y sesiones, para mutaciones: añadir hijos al agregado y
    /// persistir con SaveChanges (flujos de agendamiento y de sala/sesión).
    /// </summary>
    Task<Appointment?> GetForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Crea la cita. Traduce conflictos de concurrencia (exclusión de solapamiento / request_id único) a una violación de regla de negocio.</summary>
    Task<Appointment> AddAsync(Appointment appointment, CancellationToken ct = default);

    /// <summary>Persiste cambios de una cita cargada con <see cref="GetForUpdateAsync"/> (con control de concurrencia xmin).</summary>
    Task UpdateAsync(Appointment appointment, CancellationToken ct = default);

    /// <summary>
    /// ¿Existe una cita ACTIVA del profesional que se solape con el rango
    /// <c>[start, end)</c>? <paramref name="excludeAppointmentId"/> excluye la
    /// propia cita (reprogramación). Verificación en aplicación; la garantía
    /// real ante carreras la da el constraint de exclusión en BD.
    /// </summary>
    Task<bool> HasActiveOverlapAsync(
        Guid professionalId,
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? excludeAppointmentId = null,
        CancellationToken ct = default
    );

    /// <summary>Citas del profesional en el rango, ordenadas por inicio (agenda/calendario).</summary>
    Task<IReadOnlyList<Appointment>> ListByProfessionalAsync(
        Guid professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    );

    /// <summary>
    /// Citas de un paciente, de más reciente a más antigua (historial del paciente).
    /// </summary>
    Task<IReadOnlyList<Appointment>> ListByPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Listado administrativo global de citas con filtros opcionales
    /// (profesional, paciente, clínica, sede, estado, rango), paginado y con
    /// orden estable por inicio. Es la base de la vista "Citas" del admin.
    /// </summary>
    Task<(IReadOnlyList<Appointment> Items, int Total)> ListAdminAsync(
        Guid? professionalId,
        Guid? patientId,
        Guid? clinicId,
        Guid? locationId,
        AppointmentStatus? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>Cuenta las citas cuyo inicio cae en el rango <c>[from, to)</c>.</summary>
    Task<int> CountInRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    );

    /// <summary>
    /// Cuenta las citas cuyo inicio cae en el rango <c>[from, to)</c>,
    /// opcionalmente filtradas por profesional (KPIs del dashboard del profesional).
    /// Con <c>usePreagg</c> (default) prefiere las tablas pre-agregadas cuando
    /// tienen datos; en <c>false</c> consulta siempre las citas (exacto para
    /// rangos estrechos que tocan el presente, como hoy o próximos 7 días,
    /// donde el bucket diario no puede excluir el intradía ya pasado).
    /// </summary>
    Task<int> CountInRangeAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default,
        bool usePreagg = true
    );

    /// <summary>Cuenta las citas en un estado concreto (KPIs del dashboard admin).</summary>
    Task<int> CountByStatusAsync(AppointmentStatus status, CancellationToken ct = default);

    /// <summary>
    /// Cuenta las citas de un profesional en un estado concreto (KPIs del
    /// dashboard "Mis citas" del profesional, alcance por identidad del JWT).
    /// </summary>
    Task<int> CountByStatusAsync(
        AppointmentStatus status,
        Guid professionalId,
        CancellationToken ct = default
    );

    // --- Analytics del dashboard (gráficas; opcionalmente filtrado por profesional) ---

    /// <summary>
    /// Conteo de citas agrupadas por día en el rango <c>[from, to)</c>, con todas
    /// las horas del día UTC-0 (fecha normalizada a medianoche). Si
    /// <paramref name="professionalId"/> no es null, solo las de ese profesional.
    /// </summary>
    Task<IReadOnlyList<DailyAppointmentCount>> CountGroupedByDayAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    );

    /// <summary>
    /// Conteo de citas por estado en el rango <c>[from, to)</c> (distribución del
    /// dashboard). <paramref name="professionalId"/> opcional: perfil del profesional.
    /// </summary>
    Task<IReadOnlyList<AppointmentStatusCount>> CountGroupedByStatusAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    );

    /// <summary>
    /// Conteo de citas por hora del día en el rango <c>[from, to)</c> (franjas de
    /// mayor demanda). <paramref name="professionalId"/> opcional: perfil del profesional.
    /// </summary>
    Task<IReadOnlyList<HourlyAppointmentCount>> CountGroupedByHourAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    );

    /// <summary>
    /// Actividad agregada por profesional en el rango (solo vista admin: citas,
    /// completadas, canceladas y pacientes únicos). Sin filtro de profesional.
    /// </summary>
    Task<IReadOnlyList<ProfessionalAppointmentActivity>> CountGroupedByProfessionalAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    );

    /// <summary>Pacientes únicos con al menos una cita en el rango (atendidos).</summary>
    Task<int> CountDistinctPatientsAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    );

    /// <summary>
    /// PatientIds con cita en el rango, una entrada por cita (con duplicados):
    /// base para dimensiones que viven en la referencia del paciente (estado).
    /// <paramref name="professionalId"/> opcional: perfil del profesional.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListPatientIdsAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    );

    /// <summary>Profesionales con al menos una cita en el rango (activos).</summary>
    Task<int> CountDistinctProfessionalsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    );

    /// <summary>
    /// Métricas de llamada del rango (F5: salas, sesiones, duración sumada,
    /// reaperturas y chat por rol), atribuidas por fecha de agenda de la cita.
    /// Rollup-first: si <c>tele.appointment_daily_metrics</c> no tiene filas de
    /// estas claves en el rango, cae a conteos vivos sobre
    /// <c>tele.virtual_rooms</c>/<c>tele.telemedicine_sessions</c>/
    /// <c>tele.chat_messages</c>; las claves «solo evento» (P2) valen 0.
    /// </summary>
    Task<CallMetricsAggregate> GetCallMetricsAsync(
        Guid? professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    );

    /// <summary>Próximas citas desde <paramref name="from"/> (futuras, ordenadas por inicio).</summary>
    Task<IReadOnlyList<Appointment>> ListUpcomingAsync(
        Guid? professionalId,
        DateTimeOffset from,
        int limit,
        CancellationToken ct = default
    );

    /// <summary>
    /// Citas en <paramref name="status"/> cuyo fin programado es anterior a
    /// <paramref name="before"/>, ordenadas por fin (lectura sin tracking).
    /// Base del barrido de sesiones estancadas: la gracia efectiva por cita
    /// (settings de la organización/clínica) se aplica en el barrido.
    /// </summary>
    Task<IReadOnlyList<Appointment>> ListByStatusEndingBeforeAsync(
        AppointmentStatus status,
        DateTimeOffset before,
        CancellationToken ct = default
    );

    /// <summary>
    /// Citas en <paramref name="status"/> cuyo inicio cae en <c>[from, to)</c>,
    /// ordenadas por inicio (lectura sin tracking). Base del barrido de
    /// recordatorios: las ventanas efectivas por organización/clínica se
    /// aplican sobre las candidatas en el barrido.
    /// </summary>
    Task<IReadOnlyList<Appointment>> ListByStatusStartingBetweenAsync(
        AppointmentStatus status,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default
    );
}
