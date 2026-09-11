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

    /// <summary>Dimensión del conteo total del día.</summary>
    public const string GeneralDimension = "general";

    /// <summary>Prefijo de la dimensión horaria (<c>Hour_00</c> … <c>Hour_23</c>).</summary>
    public const string HourDimensionPrefix = "Hour_";

    /// <summary>
    /// Fila espejo global (todas las clínicas/profesionales). Mismo convenio que
    /// el processor: <c>Guid.Empty</c>.
    /// </summary>
    public static readonly Guid GlobalProfessionalId = Guid.Empty;
}
