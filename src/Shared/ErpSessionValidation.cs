using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CoppAddresd.Shared.Security;

/// <summary>Consulta Auth sin caché positiva para aplicar la suspensión en la siguiente petición.</summary>
internal sealed class ErpSessionValidation(HttpClient http)
{
    internal const string TokenKey = "erp-session-token";
    internal const string UnavailableKey = "erp-session-unavailable";

    public async Task<bool> ValidateAsync(
        ClaimsPrincipal principal,
        string token,
        CancellationToken ct
    )
    {
        if (!principal.HasClaim("aud", "erp"))
            return true;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session/validate");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return false;
        if (response.StatusCode != HttpStatusCode.NoContent)
            throw new HttpRequestException("La autoridad de acceso ERP no está disponible.");
        return true;
    }
}

internal static class ErpSessionValidationExtensions
{
    public static IServiceCollection AddErpSessionValidation(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddHttpClient<ErpSessionValidation>(http =>
        {
            http.BaseAddress = new Uri(
                configuration["AuthService:BaseUrl"]
                    ?? throw new InvalidOperationException(
                        "Falta AuthService:BaseUrl para validar sesiones ERP."
                    )
            );
            http.Timeout = TimeSpan.FromSeconds(5);
        });
        services.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme,
            options =>
            {
                var previous = options.Events.OnTokenValidated;
                options.Events.OnTokenValidated = async context =>
                {
                    await previous(context);
                    if (
                        context.Result?.Failure is not null
                        || context.Principal?.HasClaim("aud", "erp") != true
                    )
                        return;
                    var token = context.SecurityToken switch
                    {
                        JsonWebToken jwt => jwt.EncodedToken,
                        JwtSecurityToken jwt => jwt.RawData,
                        _ => "",
                    };
                    try
                    {
                        var validator =
                            context.HttpContext.RequestServices.GetRequiredService<ErpSessionValidation>();
                        if (
                            !await validator.ValidateAsync(
                                context.Principal,
                                token,
                                context.HttpContext.RequestAborted
                            )
                        )
                            context.Fail("La sesión ERP fue revocada.");
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                    {
                        context.HttpContext.Items[ErpSessionValidation.UnavailableKey] = true;
                        context.Fail("No se pudo comprobar el acceso ERP.");
                    }
                };
                var previousChallenge = options.Events.OnChallenge;
                options.Events.OnChallenge = async context =>
                {
                    if (context.HttpContext.Items.ContainsKey(ErpSessionValidation.UnavailableKey))
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                        context.Response.Headers.RetryAfter = "5";
                        return;
                    }
                    await previousChallenge(context);
                };
            }
        );
        return services;
    }
}
