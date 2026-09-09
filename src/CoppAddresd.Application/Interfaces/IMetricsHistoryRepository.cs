using CoppAddresd.Application.Features.ProgramProgress.DTOs.MetricsHistory;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio del historial de métricas clínicas del paciente
/// (metrics-history, Home del móvil): contexto crudo COMPLETO — talla del
/// perfil, catálogo ACTIVO completo, mediciones de TODAS las métricas en la
/// ventana MÁXIMA (365d paciente-local convertida a UTC, DST-aware), rangos
/// de referencia y líneas base. SOLO filas persistidas (nunca computa ni
/// dispara el motor de puntajes). Queries set-based acotadas, AsNoTracking.
///
/// El contexto se CACHEA por paciente sin codes/days (precedente
/// scores-history): el recorte a los códigos/días de cada request ocurre en
/// el handler, después del caché.
/// </summary>
public interface IMetricsHistoryRepository
{
    /// <summary>
    /// Inscripción ACTIVA del paciente (estado, no datos): el handler la
    /// verifica en CADA request, FUERA del caché — el 404 nunca se sirve de
    /// una clave caliente (anti-IDOR AC-11).
    /// </summary>
    Task<bool> HasActiveEnrollmentAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Contexto completo del historial (todas las métricas activas, ventana
    /// máxima 365d). Devuelve null solo si la inscripción activa desapareció
    /// entre el check pre-caché y este fetch (carrera → backstop del 404).
    /// </summary>
    Task<MetricsHistoryContext?> GetMetricsHistoryContextAsync(
        Guid patientId,
        CancellationToken ct = default);
}