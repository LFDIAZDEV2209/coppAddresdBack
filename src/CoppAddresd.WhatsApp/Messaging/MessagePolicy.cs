using CoppAddresd.Application.WhatsApp.Messaging;

namespace CoppAddresd.WhatsApp.Messaging;

public static class MessagePolicy
{
    public static WhatsAppFailureKind Classify(int httpStatus, int? metaCode) => metaCode == 190
        ? WhatsAppFailureKind.Permanent
        : httpStatus == 429 || httpStatus >= 500 || metaCode is 1 or 2 or 4 or 80007 or 130429
            ? WhatsAppFailureKind.Transient : WhatsAppFailureKind.Permanent;

    public static string MaskPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        return digits.Length >= 7 ? digits[..3] + "***" + digits[^3..] : "***";
    }

    public static IReadOnlyList<string> Split(string text)
    {
        List<string> parts = [];
        for (var offset = 0; offset < text.Length;)
        {
            var length = Math.Min(4096, text.Length - offset);
            if (offset + length < text.Length && char.IsHighSurrogate(text[offset + length - 1])) length--;
            parts.Add(text.Substring(offset, length));
            offset += length;
        }
        return parts;
    }
}
