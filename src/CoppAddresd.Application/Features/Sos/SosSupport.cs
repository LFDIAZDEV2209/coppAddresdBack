using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CoppAddresd.Application.Features.Sos;

/// <summary>
/// Utilidades server-side del módulo SOS: normalización E.164 del contacto de
/// emergencia y hash canónico del payload. El cliente jamás suministra número
/// ni texto de SMS (D3) — el destinatario sale de
/// <c>patient_profiles.emergency_contact.phone</c>.
/// </summary>
public static class SosSupport
{
    /// <summary>
    /// Código de país por defecto para números sin prefijo internacional
    /// (Colombia +57, convención del workspace: Auth OtpService normaliza
    /// igual). El contacto de emergencia es un campo libre del perfil.
    /// </summary>
    public const string DefaultCountryCode = "57";

    /// <summary>Longitud máxima del teléfono crudo almacenado en el JSON del contacto (schema: emergency_contact max 500).</summary>
    public const int MaxPhoneRawLength = 30;

    /// <summary>
    /// ¿El valor es estrictamente un UUIDv4 canónico (formato "D", versión 4,
    /// variante RFC 4122)? Garantiza la semántica de <c>Idempotency-Key</c>
    /// (400 si falta/inválida — REQ-SOS-01).
    /// </summary>
    public static bool IsUuidV4(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!Guid.TryParseExact(value.Trim(), "D", out var guid))
        {
            return false;
        }

        // .NET: bytes[7] contiene la versión (nibble alto == 4) y bytes[8] la
        // variante (dos bits superiores == 10b) del RFC 4122.
        var bytes = guid.ToByteArray();
        return (bytes[7] & 0xF0) == 0x40 && (bytes[8] & 0xC0) == 0x80;
    }

    /// <summary>
    /// Normaliza el teléfono del contacto a E.164 (<c>+&lt;dígitos&gt;</c>).
    /// Acepta <c>+57...</c>, <c>57...</c> o número local sin código (se asume
    /// el país por defecto). Devuelve null si el valor es inválido o no
    /// normalizable (→ 422 en la activación, REQ-SOS-03).
    /// </summary>
    public static string? NormalizePhoneE164(string? rawPhone)
    {
        if (string.IsNullOrWhiteSpace(rawPhone))
        {
            return null;
        }

        var trimmed = rawPhone.Trim();
        if (trimmed.Length > MaxPhoneRawLength)
        {
            return null;
        }

        // El contacto es free-form: acepta espacios, guiones y paréntesis.
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length is < 8 or > 15)
        {
            // E.164: máx 15 dígitos (E.164 recomendación ITU-T) y un número
            // realista nunca tiene menos de 8.
            return null;
        }

        if (
            trimmed.TrimStart().StartsWith('+')
            || digits.StartsWith(DefaultCountryCode, StringComparison.Ordinal)
        )
        {
            // Con '+' o ya con el código de país por defecto: respeta el prefijo.
            return "+" + digits;
        }

        return "+" + DefaultCountryCode + digits;
    }

    /// <summary>
    /// Extrae el teléfono crudo del JSON del contacto de emergencia
    /// (<c>{name, relationship, phone, email}</c>). Formato legacy con texto
    /// libre (no-JSON) devuelve null (no normalizable → 422).
    /// </summary>
    public static string? ExtractEmergencyContactPhone(string? emergencyContactJson)
    {
        if (string.IsNullOrWhiteSpace(emergencyContactJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(emergencyContactJson);
            if (
                document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("phone", out var phone)
                && phone.ValueKind == JsonValueKind.String
            )
            {
                return phone.GetString();
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Extrae el correo del JSON del contacto de emergencia
    /// (<c>{name, relationship, phone, email}</c>). Formato legacy o JSON sin
    /// correo devuelve null (el canal de correo se marca SinDestino).
    /// </summary>
    public static string? ExtractEmergencyContactEmail(string? emergencyContactJson)
    {
        if (string.IsNullOrWhiteSpace(emergencyContactJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(emergencyContactJson);
            if (
                document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("email", out var email)
                && email.ValueKind == JsonValueKind.String
            )
            {
                var value = email.GetString();
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Longitud máxima de un correo (RFC 5321: 320 caracteres).</summary>
    public const int MaxEmailLength = 320;

    /// <summary>
    /// Normaliza el correo del contacto de emergencia: trim, sin espacios
    /// internos, un único <c>@</c> con local y dominio no vacíos. Devuelve
    /// null si es inválido o ausente — el correo es un canal OPCIONAL: no
    /// bloquea la activación (a diferencia del teléfono), solo deja el canal
    /// en <c>SinDestino</c> sin enviar nada.
    /// </summary>
    public static string? NormalizeEmail(string? rawEmail)
    {
        if (string.IsNullOrWhiteSpace(rawEmail))
        {
            return null;
        }

        var trimmed = rawEmail.Trim();
        if (trimmed.Length > MaxEmailLength || trimmed.Any(char.IsWhiteSpace))
        {
            return null;
        }

        var at = trimmed.IndexOf('@');
        if (at <= 0 || at != trimmed.LastIndexOf('@') || at == trimmed.Length - 1)
        {
            return null;
        }

        return trimmed;
    }

    /// <summary>
    /// Hash canónico SHA-256 del payload de activación (coords + precisión +
    /// instante de captura): distingue "misma Idempotency-Key + payload
    /// idéntico" (replay → 200) de "misma clave + payload distinto" (→ 409)
    /// sin almacenar el cuerpo completo. No contiene PII (solo números).
    /// </summary>
    public static string ComputePayloadHash(
        double? latitude,
        double? longitude,
        double? accuracyMeters,
        DateTime? locationCapturedAt
    )
    {
        static string? N(double? value) =>
            value.HasValue ? value.Value.ToString("R", CultureInfo.InvariantCulture) : null;

        var canonical = string.Join(
            '|',
            "sos-v1",
            N(latitude),
            N(longitude),
            N(accuracyMeters),
            locationCapturedAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
        );

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
