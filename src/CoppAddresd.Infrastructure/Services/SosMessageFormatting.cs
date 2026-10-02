using System.Globalization;
using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Formateo compartido de los mensajes SOS enriquecidos (SMS y voz):
/// identidad del paciente, edad, documento, signos vitales (demo), enlace a
/// Maps y marca de tiempo. Solo se usan datos de la alerta y del paciente;
/// el resultado nunca se registra en logs (REQ-SOS-06).
/// </summary>
internal static class SosMessageFormatting
{
    /// <summary>
    /// Zona horaria de Colombia (UTC-5, sin horario de verano) para las marcas
    /// de tiempo legibles por el contacto de emergencia.
    /// </summary>
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    /// <summary>Nombre completo (nombre + apellidos) con fallback.</summary>
    public static string FullName(PatientProfile? patient, string fallback = "Un paciente")
    {
        if (patient is null)
        {
            return fallback;
        }

        var parts = new[] { patient.FirstName, patient.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim())
            .ToArray();

        return parts.Length == 0 ? fallback : string.Join(" ", parts);
    }

    /// <summary>Primer nombre con fallback, para el llamado a la acción.</summary>
    public static string FirstName(PatientProfile? patient, string fallback = "el paciente")
    {
        var first = patient?.FirstName?.Trim();
        return string.IsNullOrWhiteSpace(first) ? fallback : first;
    }

    /// <summary>Edad cumplida a partir de la fecha de nacimiento (null si no hay dato válido).</summary>
    public static int? Age(DateTime? dateOfBirth)
    {
        if (dateOfBirth is null)
        {
            return null;
        }

        var today = DateTime.UtcNow.Date;
        var birth = dateOfBirth.Value.Date;
        var age = today.Year - birth.Year;
        if (today < birth.AddYears(age))
        {
            age--;
        }

        return age is >= 0 and <= 130 ? age : null;
    }

    /// <summary>Fecha/hora de la activación en hora de Colombia, formato "dd/MM/yyyy HH:mm".</summary>
    public static string FormatAlertTime(DateTime createdAtUtc) =>
        createdAtUtc
            .ToUniversalTime()
            .Add(ColombiaOffset)
            .ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Enlace a Google Maps con las coordenadas y precisión, o null sin GPS.</summary>
    public static string? MapsLink(SosAlert alert)
    {
        if (alert.Latitude is null || alert.Longitude is null)
        {
            return null;
        }

        var lat = alert.Latitude.Value.ToString("F6", CultureInfo.InvariantCulture);
        var lng = alert.Longitude.Value.ToString("F6", CultureInfo.InvariantCulture);
        var accuracy = alert.AccuracyMeters is null
            ? string.Empty
            : $" (±{Math.Round(alert.AccuracyMeters.Value, MidpointRounding.AwayFromZero)} m)";

        return $"https://maps.google.com/?q={lat},{lng}{accuracy}";
    }

    /// <summary>Línea de vitales para el SMS, ej. "Vitales: FC 140 lpm · SpO2 94% · PA 160/110".</summary>
    public static string? SmsVitals(SosAlert alert)
    {
        var parts = new List<string>();
        if (alert.HeartRate is not null)
        {
            parts.Add($"FC {alert.HeartRate} lpm");
        }

        if (alert.Spo2 is not null)
        {
            parts.Add($"SpO2 {alert.Spo2}%");
        }

        if (!string.IsNullOrWhiteSpace(alert.BloodPressure))
        {
            parts.Add($"PA {alert.BloodPressure.Trim()}");
        }

        return parts.Count == 0 ? null : "Vitales: " + string.Join(" · ", parts);
    }

    /// <summary>
    /// Frase de vitales para TTS, ej. "Frecuencia cardíaca 140, saturación de
    /// oxígeno 94 por ciento, presión arterial 160 sobre 110.".
    /// </summary>
    public static string? VoiceVitals(SosAlert alert)
    {
        var parts = new List<string>();
        if (alert.HeartRate is not null)
        {
            parts.Add($"Frecuencia cardíaca {alert.HeartRate}");
        }

        if (alert.Spo2 is not null)
        {
            parts.Add($"saturación de oxígeno {alert.Spo2} por ciento");
        }

        if (!string.IsNullOrWhiteSpace(alert.BloodPressure))
        {
            parts.Add($"presión arterial {alert.BloodPressure.Trim().Replace("/", " sobre ")}");
        }

        return parts.Count == 0 ? null : string.Join(", ", parts) + ".";
    }
}
