using System.Text.Json;
using System.Text.Json.Nodes;
using CoppAddresd.Application.Services.Localization;
using CoppAddresd.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace CoppAddresd.Api.Localization;

/// <summary>La autorización y proyección originales terminan antes de traducir una copia de la respuesta.</summary>
public sealed class PatientContentLocalizationFilter(PatientContentLocalization localization, IOptions<JsonOptions> json) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var scope = context.ActionDescriptor.EndpointMetadata.OfType<LocalizePatientContentAttribute>().SingleOrDefault();
        if (scope is not null)
        {
            var headers = context.HttpContext.Response.Headers;
            AddVaryHeaders(context.HttpContext.Response);
            if (context.Result is ObjectResult { Value: not null } result && (result.StatusCode ?? 200) is >= 200 and < 300)
            {
                var language = ResolvePatientLanguage(context.HttpContext);
                if (language == "en")
                {
                    var node = JsonSerializer.SerializeToNode(result.Value, result.Value.GetType(), json.Value.JsonSerializerOptions);
                    if (node is not null)
                    {
                        try
                        {
                            result.Value = await localization.TranslateAsync(node, scope.Profile, language, context.HttpContext.RequestAborted);
                            result.DeclaredType = typeof(JsonNode);
                        }
                        catch (AiConsentException ex) when (ex.Code is "CONSENT_REQUIRED"
                            or "CONSENT_DOCUMENT_UNAVAILABLE" or "CONSENT_SUBJECT_REQUIRED")
                        {
                            // La lectura básica sigue disponible sin enviar texto a un proveedor.
                            // Se conserva el DTO original y se declara su idioma de autoría.
                            language = "es";
                        }
                    }
                }
                context.HttpContext.RequestAborted.ThrowIfCancellationRequested();
                headers["Content-Language"] = language;
            }
        }
        await next();
    }

    public static void AddVaryHeaders(HttpResponse response)
    {
        var varied = response.Headers.Vary.SelectMany(value => (value ?? "").Split(','))
            .Select(value => value.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (varied.Contains("*")) return;
        foreach (var name in new[] { "Accept-Language", "X-App-Language", "Authorization" })
            if (varied.Add(name)) response.Headers.Append("Vary", name);
    }

    /// <summary>Solo el cliente paciente actualizado solicita traducción; los editores ERP y clientes anteriores reciben la fuente.</summary>
    public static string ResolvePatientLanguage(HttpContext context, string? explicitQueryLanguage = null)
    {
        if (context.User.Identity?.IsAuthenticated != true || !context.User.HasClaim("aud", "app")) return "es";
        var query = !string.IsNullOrWhiteSpace(explicitQueryLanguage)
            ? explicitQueryLanguage.Trim() : context.Request.Query["language"].ToString().Trim();
        if (query.Length > 0) return query.Equals("en", StringComparison.OrdinalIgnoreCase) ? "en" : "es";
        // Accept-Language automático por sí solo nunca habilita la proyección.
        return !string.IsNullOrWhiteSpace(context.Request.Headers["X-App-Language"].ToString())
            ? ResolveLanguage(context.Request) : "es";
    }

    public static string ResolveLanguage(HttpRequest request)
    {
        var explicitLanguage = request.Query["language"].ToString().Trim();
        if (explicitLanguage.Length > 0) return explicitLanguage.Equals("en", StringComparison.OrdinalIgnoreCase) ? "en" : "es";
        // Un token malformado no invalida los idiomas válidos del mismo encabezado.
        // TryParse evita convertir cabeceras del cliente en errores 500.
        var selectedHeader = request.Headers["X-App-Language"];
        var preference = !string.IsNullOrWhiteSpace(selectedHeader.ToString()) ? selectedHeader : request.Headers.AcceptLanguage;
        var languages = preference.SelectMany(value => (value ?? "").Split(','))
            .Select(value => StringWithQualityHeaderValue.TryParse(value.Trim(), out var parsed) ? parsed : null)
            .Where(value => value is not null).OrderByDescending(value => value!.Quality ?? 1).ToArray();
        foreach (var entry in languages)
        {
            if ((entry!.Quality ?? 1) <= 0) continue;
            var language = entry.Value.Value?.Split('-')[0].ToLowerInvariant();
            if (language is "en" or "es") return language;
            if (language == "*")
            {
                // El comodín se aplica solo a idiomas sin preferencia explícita;
                // nunca revive un q=0 ni supera la calidad del rango específico.
                foreach (var fallback in new[] { "es", "en" })
                    if (!languages.Any(candidate => candidate!.Value.Value?.Split('-')[0].Equals(fallback, StringComparison.OrdinalIgnoreCase) == true))
                        return fallback;
            }
        }
        return "es";
    }
}
