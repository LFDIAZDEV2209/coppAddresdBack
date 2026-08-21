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