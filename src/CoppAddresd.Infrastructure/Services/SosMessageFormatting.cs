using System.Globalization;
using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Formateo compartido de mensajes SOS: identidad del paciente, lecturas
/// reportadas, enlaces y fecha. Solo se usan datos de la alerta y del paciente;
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
            .Select(part => SingleLine(part!))
            .ToArray();

        return parts.Length == 0 ? fallback : string.Join(" ", parts);
    }

    /// <summary>Primer nombre con fallback, para el llamado a la acción.</summary>
    public static string FirstName(PatientProfile? patient, string fallback = "el paciente")
    {
        var first = patient?.FirstName is { } value ? SingleLine(value) : null;
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

    /// <summary>Hora universal explícita: sin zona del dispositivo no se inventa una hora local.</summary>
    public static string FormatMessageTime(DateTime createdAtUtc)
    {
        var utc = createdAtUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)
            : createdAtUtc.ToUniversalTime();
        return utc.ToString("dd MMM yyyy · HH:mm 'UTC'", CultureInfo.GetCultureInfo("es"));
    }

    /// <summary>Hora real de captura; no se sustituye por la hora de activación.</summary>
    public static string LocationCaptureTime(SosAlert alert) =>
        alert.LocationCapturedAt is { } captured && captured > DateTime.UnixEpoch && captured <= alert.CreatedAt.AddMinutes(1)
            ? "GPS capturado: " + FormatMessageTime(captured)
            : "Hora de captura no disponible";

    public static string LocationAccuracy(SosAlert alert) =>
        alert.AccuracyMeters is { } value && double.IsFinite(value) && value >= 0 && value <= 100_000
            ? "Precisión GPS aprox.: " + (value < 1000
                ? Math.Max(1, Math.Ceiling(value)).ToString("0", CultureInfo.InvariantCulture) + " m"
                : (value / 1000).ToString("0.#", CultureInfo.GetCultureInfo("es-CO")) + " km")
            : "Precisión GPS no disponible";

    /// <summary>Enlace válido de Google Maps; la precisión no forma parte de una URL.</summary>
    public static string? MapsLink(SosAlert alert)
    {
        if (alert.Latitude is not { } latitude || alert.Longitude is not { } longitude
            || !double.IsFinite(latitude) || !double.IsFinite(longitude)
            || latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return null;
        }

        var lat = latitude.ToString("0.######", CultureInfo.InvariantCulture);
        var lng = longitude.ToString("0.######", CultureInfo.InvariantCulture);
        return $"https://maps.google.com/?q={lat},{lng}";
    }

    /// <summary>Admite un enlace HTTPS emitido por el servidor; nunca esquemas ejecutables.</summary>
    public static string? LocationLink(SosAlert alert, string? locationUrl = null) =>
        Uri.TryCreate(locationUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(uri.UserInfo)
        && !locationUrl!.Any(char.IsControl)
            ? uri.AbsoluteUri
            : MapsLink(alert);

    /// <summary>Teléfono del paciente, distinto del destinatario de la alerta; no adivina el país.</summary>
    public static string? PatientPhone(PatientProfile? patient)
    {
        if (string.IsNullOrWhiteSpace(patient?.PhoneNumber)) return null;
        var raw = patient.PhoneNumber.Trim();
        if (raw.Skip(1).Contains('+')) return null;
        if (raw.Any(character => !char.IsAsciiDigit(character) && character is not ('+' or ' ' or '-' or '(' or ')' or '.')))
        {
            return null;
        }
        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        if (!raw.StartsWith('+'))
        {
            var prefix = patient.PhoneCountryCode?.Trim().TrimStart('+');
            if (string.IsNullOrEmpty(prefix) || prefix.Length > 3 || !prefix.All(char.IsAsciiDigit)) return null;
            digits = prefix + digits;
        }
        return digits.Length is >= 8 and <= 15 && digits[0] != '0' ? "+" + digits : null;
    }

    /// <summary>Únicamente lecturas persistidas; nunca completa datos faltantes.</summary>
    public static IReadOnlyList<(string Label, string Value)> ReportedVitals(SosAlert alert)
    {
        var parts = new List<(string, string)>();
        if (alert.HeartRate is not null) parts.Add(("Pulso", $"{alert.HeartRate} lpm"));
        if (alert.Spo2 is not null)
        {
            parts.Add(("Oxígeno", $"{alert.Spo2}%"));
        }

        if (!string.IsNullOrWhiteSpace(alert.BloodPressure))
        {
            parts.Add(("Presión", $"{SingleLine(alert.BloodPressure)} mmHg"));
        }
        return parts;
    }

    public static string? SmsVitals(SosAlert alert)
    {
        var readings = ReportedVitals(alert);
        return readings.Count == 0 ? null : "Lecturas compartidas: " + string.Join(" · ", readings.Select(reading => $"{reading.Label} {reading.Value}"));
    }

    public static string SingleLine(string value) => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

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
