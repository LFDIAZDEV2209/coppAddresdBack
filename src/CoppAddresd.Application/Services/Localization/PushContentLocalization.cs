using System.Text.RegularExpressions;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;

namespace CoppAddresd.Application.Services.Localization;

public sealed record PushContent(string Title, string Body);

/// <summary>Proyección exclusiva para la entrega: conserva el contenido canónico de notificaciones, chat y auditoría.</summary>
public static class PushContentLocalization
{
    public static string Language(DeviceToken device) => device.Language == "en" ? "en" : "es";

    public static async Task<IReadOnlyDictionary<string, PushContent>> PrepareAsync(
        IReadOnlyCollection<DeviceToken> devices, string title, string body,
        IContentTranslationService? translations, CancellationToken ct,
        string? englishTitle = null, string? englishBody = null)
    {
        var result = new Dictionary<string, PushContent> { ["es"] = new(title, body) };
        if (!devices.Any(device => Language(device) == "en")) return result;
        var titleEn = string.IsNullOrWhiteSpace(englishTitle) ? ReviewedEnglish(title) : englishTitle;
        var bodyEn = string.IsNullOrWhiteSpace(englishBody) ? ReviewedEnglish(body) : englishBody;
        var missing = new[] { titleEn is null ? title : null, bodyEn is null ? body : null }
            .OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
        if (missing.Length > 0)
        {
            if (translations is null) throw new ContentTranslationUnavailableException();
            var translated = await translations.TranslateAsync(missing, "en", ct);
            string Resolve(string source) => translated.TryGetValue(source, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value : throw new ContentTranslationUnavailableException();
            titleEn ??= Resolve(title);
            bodyEn ??= Resolve(body);
        }
        result["en"] = new(titleEn!, bodyEn!);
        return result;
    }

    // La coincidencia exacta con la fuente invalida el texto revisado cuando cambia el texto configurable.
    private static readonly IReadOnlyDictionary<string, string> Reviewed = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["🚨 Alerta SOS activa"] = "🚨 Active SOS alert",
        ["Un paciente activó su alerta SOS de emergencia. Revísala de inmediato en la plataforma."] =
            "A patient activated an emergency SOS alert. Review it immediately in the platform.",
        ["✅ Día perfecto"] = "✅ Perfect day",
        ["💊 ¡Nutriobiótico!"] = "💊 Nutriobiotic!",
        ["💬 ¡Llegaste al día 7!"] = "💬 You reached day 7!",
        ["¡Hola! Ya completaste 7 días del programa. 💪 Queremos saber cómo te sentís: contanos si pudiste cumplir tus tareas diarias y qué barreras encontraste. Si algo se te dificulta, escribinos y te ayudamos a ajustar el plan. ¡Tu constancia ya se nota! Y si ya tenés análisis de laboratorio, subilos al chat y los revisamos juntos."] = "Hello! You have completed 7 days of the program. 💪 We would like to know how you feel: tell us whether you were able to complete your daily tasks and what barriers you encountered. If anything is difficult, write to us and we will help you adjust the plan. Your consistency is already showing! And if you have laboratory results, upload them to the chat and we will review them together.",
        ["🌟 ¡14 días de progreso!"] = "🌟 14 days of progress!",
        ["¡Dos semanas adentro del programa! 🎉 Queremos saber cómo viene tu energía y tus hábitos. Recordá que los pequeños ajustes a tiempo hacen grandes diferencias: si tenés dudas sobre tu rutina, contanos y te orientamos. Si tenés análisis recientes, subilos al chat para ir viendo cómo avanza tu progreso."] = "Two weeks into the program! 🎉 We would like to know how your energy and habits are progressing. Remember that small, timely adjustments make a big difference: if you have questions about your routine, tell us and we will guide you. If you have recent laboratory results, upload them to the chat so we can follow your progress.",
        ["💊 Día 21: revisemos tus hábitos"] = "💊 Day 21: let's review your habits",
        ["¡Llegaste al día 21! 🏆 Es un gran momento para revisar cómo te sentís con el plan. ¿Notaste cambios? Si algo no te funciona o tenés molestias, te recomendamos agendar una cita con tu profesional para ajustar el rumbo a tiempo. Y si tenés los últimos análisis a mano, subilos al chat y los comparamos con tu evolución."] = "You reached day 21! 🏆 This is a great time to review how you feel about the plan. Have you noticed changes? If something is not working for you or you have discomfort, we recommend scheduling an appointment with your healthcare professional to make adjustments in time. And if you have your latest laboratory results available, upload them to the chat and we will compare them with your progress.",
        ["📊 ¡Día 45! Momento de evaluar"] = "📊 Day 45! Time to evaluate",
        ["¡Ya llevás 45 días! 📈 Este es un punto clave del programa. ¿Cómo te sentís con tus resultados hasta ahora? Si querés, agendá una consulta para revisar tu progreso y planear juntos los próximos pasos. Y si tenés los últimos análisis a mano, subilos al chat y los comparamos con tu progreso."] = "You have reached 45 days! 📈 This is a key point in the program. How do you feel about your results so far? If you would like, schedule a consultation to review your progress and plan the next steps together. And if you have your latest laboratory results available, upload them to the chat and we will compare them with your progress.",
        ["⚡ Día 60: casi en la meta"] = "⚡ Day 60: almost at the finish line",
        ["¡60 días de constancia! ⚡ Felicitaciones por llegar hasta acá. Contanos cómo te sentís: si notás estancamiento o fatiga, una cita con tu profesional puede ayudarte a retomar el impulso. ¡No aflojes! Y si te quedaron análisis por subir, hacelo desde el chat y vemos cómo vas."] = "60 days of consistency! ⚡ Congratulations on getting this far. Tell us how you feel: if you notice a plateau or fatigue, an appointment with your healthcare professional can help you regain momentum. Keep going! And if you still have laboratory results to upload, do it through the chat so we can see how you are doing.",
        ["🏁 ¡Día 90! Gran hito del programa"] = "🏁 Day 90! A major program milestone",
        ["¡Completaste 90 días del programa! 🏁 Es momento de evaluar tus resultados de cerca: agendá una cita para revisar tus métricas y decidir cómo seguir cuidándote. ¡Felicitaciones por tu compromiso! Y si querés, subí tus últimos análisis al chat para compararlos con todo tu recorrido."] = "You have completed 90 days of the program! 🏁 It is time to evaluate your results closely: schedule an appointment to review your metrics and decide how to continue taking care of yourself. Congratulations on your commitment! And if you would like, upload your latest laboratory results to the chat to compare them with your entire journey.",
    };

    internal static string? ReviewedEnglish(string source)
    {
        if (Reviewed.TryGetValue(source, out var value)) return value;
        // Solo coinciden las plantillas redactadas y ancladas al texto completo. Las capturas numéricas se copian literalmente.
        var patterns = new (string Pattern, string Replacement)[]
        {
            (@"^✅ Día perfecto · \+(\d+) XP$", "✅ Perfect day · +$1 XP"),
            (@"^⭐ ¡Nivel (\d+)!$", "⭐ Level $1!"),
            (@"^⭐ ¡Subiste a Nivel (\d+)!$", "⭐ You reached level $1!"),
            (@"^🏆 ¡(\d+) días!$", "🏆 $1 days!"),
            (@"^🏆 ¡(\d+) días! \+(\d+) XP$", "🏆 $1 days! +$2 XP"),
            (@"^🏆 ¡(\d+) días! \+(\d+) XP · ¡x2 por (\d+)h!$", "🏆 $1 days! +$2 XP · x2 for $3h!"),
            (@"^💊 ¡(\d+) días tomando tu Nutriobiótico!$", "💊 $1 days taking your Nutriobiotic!"),
            (@"^Control del día (\d+)$", "Day $1 check-in"),
            (@"^Te quedó pendiente tu control del día (\d+): ¿pudiste pasar tus últimos análisis por el chat\? Si ya no querés subirlos, avisáme y lo cerramos\.$",
                "Your day $1 check-in is still pending: were you able to send your latest laboratory results through the chat? If you no longer want to upload them, let me know and we will close it.")
        };
        foreach (var (pattern, replacement) in patterns)
        {
            if (Regex.IsMatch(source, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                return Regex.Replace(source, pattern, replacement, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
        return null;
    }
}
