using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoppAddresd.Application.WhatsApp.Binding;

namespace CoppAddresd.WhatsApp.Binding;

public sealed class AuthOtpClient(HttpClient client) : IWhatsAppOtpClient
{
    public async Task<OtpLookupResult> LookupAsync(string document, CancellationToken ct)
    {
        try
        {
            using var response = await client.PostAsJsonAsync("api/auth/id-lookup", new { documentNumber = document, application = "app" }, ct);
            var outcome = Classify(response.StatusCode);
            if (outcome != OtpOutcome.Approved) return new(outcome);
            using var json = await ReadAsync(response, ct);
            var root = json.RootElement;
            if (!root.TryGetProperty("patientId", out var patient) || !patient.TryGetGuid(out var id)
                || !root.TryGetProperty("contacts", out var contacts) || contacts.ValueKind != JsonValueKind.Array) return new(OtpOutcome.Rejected);
            var phone = contacts.EnumerateArray().Where(c => c.TryGetProperty("id", out var contact)
                && contact.GetString() == "phone" && c.TryGetProperty("type", out var type)
                && string.Equals(type.GetString(), "PHONE", StringComparison.OrdinalIgnoreCase)).ToList();
            return phone.Count == 1 ? new(OtpOutcome.Approved, new(id, "phone")) : new(OtpOutcome.Rejected);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException or InvalidOperationException or FormatException)
        { return new(OtpOutcome.Unavailable); }
    }

    public Task<OtpOutcome> SendAsync(string document, string contactId, string language, CancellationToken ct) =>
        SendCoreAsync(document, contactId, language, ct);

    public async Task<OtpVerificationResult> VerifyAsync(string document, string code, CancellationToken ct)
    {
        try
        {
            using var response = await client.PostAsJsonAsync("api/auth/verify-otp",
                new { documentNumber = document, otp = code, contactId = "phone", application = "app", rememberMe = false }, ct);
            var outcome = Classify(response.StatusCode);
            if (outcome != OtpOutcome.Approved) return new(outcome);
            using var json = await ReadAsync(response, ct);
            if (!json.RootElement.TryGetProperty("verifiedContactId", out var channel) || channel.GetString() != "phone")
                return new(OtpOutcome.Unavailable);
            if (!json.RootElement.TryGetProperty("accessToken", out var token) || token.ValueKind != JsonValueKind.String) return new(OtpOutcome.Unavailable);
            // La respuesta procede del Auth configurado, no del paciente. Leer solo el sujeto para
            // cotejarlo con el user_id recién aprovisionado; no usar ni almacenar el token como sesión.
            var parts = (token.GetString() ?? "").Split('.');
            if (parts.Length != 3) return new(OtpOutcome.Unavailable);
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
            using var claims = JsonDocument.Parse(Convert.FromBase64String(payload));
            foreach (var key in new[] { "sub", "nameid", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" })
                if (claims.RootElement.TryGetProperty(key, out var subject) && Guid.TryParse(subject.GetString(), out var userId))
                    return new(OtpOutcome.Approved, userId);
            return new(OtpOutcome.Unavailable);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException or InvalidOperationException or FormatException)
        { return new(OtpOutcome.Unavailable); }
    }

    private async Task<OtpOutcome> SendCoreAsync(string document, string contactId, string language, CancellationToken ct)
    {
        try
        {
            using var response = await client.PostAsJsonAsync("api/auth/send-otp", new { documentNumber = document, contactId, language }, ct);
            return Classify(response.StatusCode);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException or InvalidOperationException)
        { return OtpOutcome.Unavailable; }
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, CancellationToken ct) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

    private static OtpOutcome Classify(HttpStatusCode status) => status switch
    {
        >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices => OtpOutcome.Approved,
        HttpStatusCode.TooManyRequests => OtpOutcome.RateLimited,
        >= HttpStatusCode.InternalServerError => OtpOutcome.Unavailable,
        _ => OtpOutcome.Rejected,
    };
}
