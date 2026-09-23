using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Application.Interfaces;

/// <summary>Conteo de citas agrupadas por día (serie temporal del dashboard).</summary>
public sealed record DailyAppointmentCount(DateTimeOffset Day, int Count);

/// <summary>Conteo de citas por estado (distribución del dashboard).</summary>
public sealed record AppointmentStatusCount(AppointmentStatus Status, int Count);

/// <summary>Conteo de citas por hora del día (franjas de mayor demanda).</summary>
public sealed record HourlyAppointmentCount(int Hour, int Count);

/// <summary>Actividad agregada de un profesional en un rango (solo vista admin).</summary>
public sealed record ProfessionalAppointmentActivity(
    Guid ProfessionalId,
    int Total,
    int Completed,
    int Cancelled,
    int UniquePatients);

/// <summary>
/// Agregado de métricas de llamada (F5) para el dashboard: salas, sesiones
/// (con la suma de duración para calcular el promedio en lectura), reaperturas
/// y chat por rol. Las claves P2 (<c>join_tokens_issued</c>,
/// <c>participant_connections</c>) son «solo evento»: sin filas en el rollup
/// valen 0.
/// </summary>
public sealed record CallMetricsAggregate(
    int RoomsOpened,
    int SessionsStarted,
    int SessionsEnded,
    long TotalDurationSeconds,
    int Reopens,
    IReadOnlyDictionary<string, int> ChatMessagesByRole,
    int JoinTokensIssued,
    IReadOnlyDictionary<string, int> ParticipantConnectionsByRole);