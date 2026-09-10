namespace CoppAddresd.Application.Features.ProgramProgress.Events;

/// <summary>
/// Marcador para eventos de métricas biométricas clínicas procesados en background (0ms overhead).
/// </summary>
public interface IBiometriaMetricEvent;

/// <summary>
/// Evento emitido al persistir mediciones biométricas (tarea vitals) desde la app móvil.
/// </summary>
public sealed record BiometriaMeasuredEvent(
    Guid PatientId,
    string? Gender,
    string? CityId,
    DateOnly ObservedDate,
    decimal? Imc,
    decimal? BodyFatPct,
    decimal? GlucosaFasting,
    decimal? Waist,
    decimal? Hip
) : IBiometriaMetricEvent;
