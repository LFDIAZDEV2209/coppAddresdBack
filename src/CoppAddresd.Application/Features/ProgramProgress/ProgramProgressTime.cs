namespace CoppAddresd.Application.Features.ProgramProgress;

/// <summary>
/// Utilidades de tiempo local del paciente compartidas por los handlers del
/// módulo Progreso del Programa (SPEC §6.11): toda la matemática de días y
/// semanas se computa en la zona IANA del paciente, nunca en UTC del servidor.
/// Espeja los helpers privados del repositorio (<c>ProgramRepository</c>); aquí
/// viven los que los casos de uso de aplicación necesitan (default de
/// <c>startLocalDate</c> al inscribir y validación de zonas IANA en los
/// validadores FluentValidation).
/// </summary>
internal static class ProgramProgressTime
{
    /// <summary>Lunes = 1 … Domingo = 7 (mismo índice que NutritionPlanDay).</summary>
    public static short ToIsoWeekday(DateOnly date)
        => (short)(((int)date.DayOfWeek + 6) % 7 + 1);

    /// <summary>Lunes de la semana que contiene <paramref name="date"/>.</summary>
    public static DateOnly MondayOfWeek(DateOnly date)
        => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    /// <summary>
    /// Fecha local de hoy del paciente desde su zona IANA. Fallback a UTC si la
    /// zona no está disponible en el SO (Windows mapea las IANA comunes).
    /// </summary>
    public static DateOnly PatientLocalToday(string timezone)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow.Date);
        }
        catch (InvalidTimeZoneException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow.Date);
        }
    }

    /// <summary>
    /// ¿Es una zona horaria IANA válida? <c>TimeZoneInfo</c> en .NET (Windows con
    /// puente ICU) resuelve los nombres IANA comunes; si el SO no conoce la zona,
    /// la inscripción se rechaza en validación (400) antes de persistir.
    /// </summary>
    public static bool IsValidIanaTimezone(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            return false;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}