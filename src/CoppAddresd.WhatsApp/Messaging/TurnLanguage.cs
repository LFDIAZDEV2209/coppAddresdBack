using System.Globalization;
using System.Text;

namespace CoppAddresd.WhatsApp.Messaging;

public static class TurnLanguage
{
    public static string Normalize(string text) => new string(text.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
        .Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();

    public static bool? OptOutCommand(string text) => Normalize(text) switch
    {
        "salir" or "stop" => true,
        "iniciar" or "start" => false,
        _ => null,
    };

    public static string Detect(string text, string previous = "es")
    {
        var value = " " + Normalize(text) + " ";
        if (new[] { " hello", " hi ", "how are", " i feel", " i'm", " i am", " please", "thank you", " my plan", "speak english", " stop ", " start " }.Any(value.Contains)) return "en";
        if (new[] { " hola", "como estas", " me siento", " mi plan", " gracias", " por favor", "espanol", " salir ", " iniciar " }.Any(value.Contains)) return "es";
        return previous is "en" ? "en" : "es";
    }
}
