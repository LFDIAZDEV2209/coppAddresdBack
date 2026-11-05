using System.Text.RegularExpressions;

namespace CoppAddresd.Domain.ValueObjects;

/// <summary>Regla única de teléfonos para guardar contactos y despachar SOS.</summary>
public static class EmergencyContactPhone
{
    public const string DefaultCountryCode = "57";
    public const int MaxRawLength = 30;

    public static string? Normalize(string? value)
    {
        var raw = value?.Trim() ?? "";
        if (raw.Length is 0 or > MaxRawLength || !Regex.IsMatch(raw, @"^\+?[0-9().\s-]+$")) return null;
        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is < 8 or > 15) return null;
        var phone = raw.StartsWith('+') || (digits.StartsWith(DefaultCountryCode, StringComparison.Ordinal) && digits.Length == 12)
            ? "+" + digits : "+" + DefaultCountryCode + digits;
        if (!Regex.IsMatch(phone, @"^\+[1-9]\d{7,14}$")) return null;
        if (phone.StartsWith("+57", StringComparison.Ordinal) && phone.Length != 13) return null;
        if (phone.StartsWith("+1", StringComparison.Ordinal) && phone.Length != 12) return null;
        return phone;
    }
}
