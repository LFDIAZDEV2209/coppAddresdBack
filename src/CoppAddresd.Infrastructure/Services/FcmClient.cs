using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Cliente de Firebase Cloud Messaging (API v1). Obtiene un access token
/// OAuth2 firmando un JWT (RS256) con el service account y hace POST a
/// <c>/v1/projects/{projectId}/messages:send</c>.
///
/// Degradado por diseño: si <c>Fcm:Enabled</c> es false o faltan credenciales,
/// devuelve <see cref="FcmSendStatus.Disabled"/> sin lanzar (el endpoint nunca
/// cae por FCM no configurado). Los tokens obsoletos (UNREGISTERED /
/// NotRegistered) se marcan con <see cref="FcmSendStatus.TokenInvalid"/> para
/// que el caller los elimine.
/// </summary>
public class FcmClient : IFcmClient
{
    private const string BaseUrl = "https://fcm.googleapis.com";
    private const string MessagesSendPath = "/v1/projects/{0}/messages:send";
    private const string OAuthGrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer";
    private const int TokenCacheSafetyMarginSeconds = 300; // expira ~5 min antes (~55 min útiles)

    // Los typed clients (AddHttpClient) son transitorios: el cache debe ser
    // estático para sobrevivir entre instancias y evitar un OAuth por request.
    private static readonly ConcurrentDictionary<string, CachedToken> AccessTokenCache = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        // El body de FCM no admite `data: null`: los campos sin valor se omiten.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly FcmSettings _settings;
    private readonly ILogger<FcmClient> _logger;

    public FcmClient(
        HttpClient httpClient,
        IOptions<FcmSettings> settings,
        ILogger<FcmClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<FcmSendResult> SendAsync(
        string deviceToken,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        // Operación degradada: FCM no configurado (dev) — nunca romper el flujo.
        if (!_settings.Enabled)
        {
            _logger.LogWarning(
                "FCM deshabilitado (Fcm:Enabled=false) — push NO enviado a {TokenHash}.",
                HashToken(deviceToken));
            return new FcmSendResult(FcmSendStatus.Disabled);
        }

        if (string.IsNullOrWhiteSpace(_settings.ProjectId)
            || string.IsNullOrWhiteSpace(_settings.ServiceAccountJson))
        {
            _logger.LogWarning(
                "FCM sin credenciales (ProjectId/ServiceAccountJson vacíos) — push NO enviado a {TokenHash}.",
                HashToken(deviceToken));
            return new FcmSendResult(FcmSendStatus.Disabled);
        }

        var accessToken = await GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogError("No se pudo obtener access token OAuth2 para FCM.");
            return new FcmSendResult(FcmSendStatus.Error, "auth", "No se pudo autenticar contra FCM.");
        }

        var payload = new
        {
            message = new
            {
                token = deviceToken,
                notification = new { title, body },
                android = new
                {
                    priority = "high",
                    notification = new
                    {
                        channelId = "default",
                    },
                },
                data = data is { Count: > 0 } ? data : null,
            },
        };

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                string.Format(MessagesSendPath, _settings.ProjectId))
            {
                Content = JsonContent.Create(payload, options: JsonOpts),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Push FCM enviado a {TokenHash}.", HashToken(deviceToken));
                return new FcmSendResult(FcmSendStatus.Sent);
            }

            // Tokens obsoletos: FCM responde 404 con UNREGISTERED o 400 con
            // NotRegistered — el token debe eliminarse.
            if (IsInvalidTokenResponse(response, responseBody))
            {
                _logger.LogWarning(
                    "Token FCM obsoleto ({Status}): {TokenHash} — el caller debe eliminarlo.",
                    (int)response.StatusCode, HashToken(deviceToken));
                return new FcmSendResult(FcmSendStatus.TokenInvalid, "UNREGISTERED", responseBody);
            }

            _logger.LogWarning(
                "FCM rechazó el push: {Status} {Body} (token {TokenHash}).",
                (int)response.StatusCode, responseBody, HashToken(deviceToken));
            return new FcmSendResult(FcmSendStatus.Error, ((int)response.StatusCode).ToString(), responseBody);
        }
        catch (Exception ex)
        {
            // Error de red/transporte: no derribar el endpoint, se reporta como error.
            _logger.LogError(ex, "Error al enviar push FCM a {TokenHash}.", HashToken(deviceToken));
            return new FcmSendResult(FcmSendStatus.Error, "network", ex.Message);
        }
    }

    /// <summary>
    /// Obtiene (con cache ~55 min) el access token OAuth2 de Firebase. Usa el
    /// JWT firmado con el service account y el token_uri del mismo.
    /// </summary>
    private async Task<string?> GetAccessTokenAsync(CancellationToken ct)
    {
        if (AccessTokenCache.TryGetValue(_settings.ProjectId, out var cached)
            && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Token;
        }

        var credential = GoogleJwtHelper.ResolveServiceAccount(_settings.ServiceAccountJson);
        if (credential is null)
        {
            _logger.LogError("Service account FCM inválido o ilegible (client_email/private_key faltantes).");
            return null;
        }

        var assertion = GoogleJwtHelper.CreateJwt(credential);
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, credential.TokenUri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = OAuthGrantType,
                ["assertion"] = assertion,
            }),
        };

        using var tokenResponse = await _httpClient.SendAsync(tokenRequest, ct);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorBody = await tokenResponse.Content.ReadAsStringAsync(ct);
            _logger.LogError("Google OAuth2 rechazó el JWT: {Status} {Body}.", (int)tokenResponse.StatusCode, errorBody);
            return null;
        }

        var result = await tokenResponse.Content.ReadFromJsonAsync<OAuthTokenResponseJson>(JsonOpts, ct);
        if (string.IsNullOrWhiteSpace(result?.AccessToken))
        {
            _logger.LogError("Google OAuth2 no devolvió access_token.");
            return null;
        }

        var expiresIn = result.ExpiresIn > 0 ? result.ExpiresIn : 3600;
        var expiresAt = DateTimeOffset.UtcNow
            .AddSeconds(Math.Max(60, expiresIn - TokenCacheSafetyMarginSeconds));

        AccessTokenCache[_settings.ProjectId] = new CachedToken(result.AccessToken, expiresAt);
        return result.AccessToken;
    }

    /// <summary>
    /// Detecta respuestas de token obsoleto de FCM: 404 con UNREGISTERED o
    /// 400 con NotRegistered en el body (documentación FCM API v1).
    /// </summary>
    private static bool IsInvalidTokenResponse(HttpResponseMessage response, string responseBody)
    {
        if (response.StatusCode is not (System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound))
            return false;

        return responseBody.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase)
            || responseBody.Contains("NotRegistered", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Hash corto del token para logs: el token FCM es un identificador opaco
    /// (no PHI), pero se evita exponerlo completo en los logs.
    /// </summary>
    private static string HashToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return "<empty>";

        return token.Length <= 12
            ? token
            : $"{token[..6]}...{token[^6..]}";
    }

    // Contrato de Google OAuth2 token endpoint (`access_token`, `expires_in`).
    private sealed record OAuthTokenResponseJson(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn = 0);

    private sealed record CachedToken(string Token, DateTimeOffset ExpiresAt);
}