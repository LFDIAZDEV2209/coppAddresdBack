using CoppAddresd.Application.WhatsApp.Messaging;

namespace CoppAddresd.WhatsApp.Messaging;

public static class MessagePolicy
{
    public static WhatsAppFailureKind Classify(int httpStatus, int? metaCode) =>
        metaCode == 190 ? WhatsAppFailureKind.Permanent
        : httpStatus == 429 || httpStatus >= 500 || metaCode is 1 or 2 or 4 or 80007 or 130429
            ? WhatsAppFailureKind.Transient
        : WhatsAppFailureKind.Permanent;

    public static string MaskPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        return digits.Length >= 7 ? digits[..3] + "***" + digits[^3..] : "***";
    }

    public static IReadOnlyList<string> Split(string text)
    {
        List<string> parts = [];
        for (var offset = 0; offset < text.Length; )
        {
            var length = Math.Min(4096, text.Length - offset);
            if (offset + length < text.Length && char.IsHighSurrogate(text[offset + length - 1]))
                length--;
            parts.Add(text.Substring(offset, length));
            offset += length;
        }
        return parts;
    }

    /// <summary>
    /// Ajusta la respuesta del agente al formato que WhatsApp soporta: negrita
    /// con asteriscos simples (los dobles de Markdown se ven literales), sin
    /// separadores horizontales (---) que WhatsApp no renderiza y encabezados
    /// Markdown convertidos a negrita simple.
    /// </summary>
    public static string NormalizeForWhatsApp(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            // Separadores horizontales de Markdown: WhatsApp los muestra literales.
            if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\s*([-_*]\s*){3,}$"))
            {
                lines[i] = string.Empty;
                continue;
            }
            // Encabezados Markdown (# Título) → negrita simple de WhatsApp.
            var header = System.Text.RegularExpressions.Regex.Match(
                line,
                @"^\s*#{1,6}\s+(.+?)\s*$"
            );
            if (header.Success)
            {
                lines[i] = "*" + header.Groups[1].Value + "*";
                continue;
            }
            // Negrita doble de Markdown (**texto**) → negrita simple de WhatsApp.
            lines[i] = System.Text.RegularExpressions.Regex.Replace(line, @"\*\*(.+?)\*\*", "*$1*");
        }
        return string.Join("\n", lines);
    }
}
