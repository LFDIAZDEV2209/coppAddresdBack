using System.Globalization;
using System.Text;

namespace CoppAddresd.Application.DTOs;

/// <summary>Attributed reference demonstrations already curated by the patient app.
/// An uploaded exercise medium takes precedence. Unknown names have no automatic match.</summary>
public sealed record ExerciseReferenceVideo(string EmbedUrl, string SourceUrl, string Author)
{
    public static ExerciseReferenceVideo? Resolve(string? youTubeUrl, string name)
        => string.IsNullOrWhiteSpace(youTubeUrl) ? ForExercise(name) : new(
            "https://www.youtube-nocookie.com/embed/" + YouTubeLink.Id(youTubeUrl),
            YouTubeLink.Canonical(youTubeUrl)!, "YouTube");

    public static ExerciseReferenceVideo? ForExercise(string name)
    {
        var normalized = new string(name.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray()).ToLowerInvariant().Trim();
        return normalized switch
        {
            "marcha en el lugar" or "marcha en el sitio" => new(
                "https://player.vimeo.com/video/418873416?dnt=1",
                "https://www.lancsteachinghospitals.nhs.uk/trauma-exercises",
                "Lancashire Teaching Hospitals · NHS"),
            "sentarse y levantarse" or "sentarse y levantarse de la silla" => new(
                "https://www.youtube-nocookie.com/embed/0FkRr45YeEQ",
                "https://www.youtube.com/watch?v=0FkRr45YeEQ", "NHS Ayrshire & Arran"),
            "plancha" or "plancha abdominal" or "plancha isometrica" => new(
                "https://www.youtube-nocookie.com/embed/cCMob-mi19U",
                "https://www.youtube.com/watch?v=cCMob-mi19U", "Smart Fit Chile"),
            "estocada" or "estocadas" => new(
                "https://www.youtube-nocookie.com/embed/3U9IbwjkOfk",
                "https://www.youtube.com/watch?v=3U9IbwjkOfk", "Smart Fit Chile"),
            _ => null,
        };
    }
}

/// <summary>Solo hosts y rutas oficiales; no acepta iframes arbitrarios.</summary>
public static class YouTubeLink
{
    public static string? Canonical(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return "https://www.youtube.com/watch?v=" + Id(value);
    }

    public static string Id(string value)
    {
        if (value.Length > 2048 || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort)
            throw new CoppAddresd.Domain.Exceptions.UnprocessableEntityException("Usa un enlace válido de YouTube con HTTPS.");
        string? id = null;
        var host = uri.Host.ToLowerInvariant();
        var path = uri.AbsolutePath.Trim('/').Split('/');
        if (host == "youtu.be" && path.Length == 1) id = path[0];
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "www.youtube-nocookie.com")
        {
            if (uri.AbsolutePath == "/watch")
                id = uri.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2))
                    .FirstOrDefault(p => p.Length == 2 && p[0] == "v")?.ElementAtOrDefault(1);
            else if (path.Length == 2 && path[0] is "embed" or "shorts" or "live") id = path[1];
        }
        if (id is null || !System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-zA-Z0-9_-]{11}$"))
            throw new CoppAddresd.Domain.Exceptions.UnprocessableEntityException("Usa un enlace válido de YouTube.");
        return id;
    }
}
