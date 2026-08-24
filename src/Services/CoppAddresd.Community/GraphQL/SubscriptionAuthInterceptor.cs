using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HotChocolate.AspNetCore;
using HotChocolate.AspNetCore.Subscriptions;
using HotChocolate.AspNetCore.Subscriptions.Protocols;
using Microsoft.IdentityModel.Tokens;

namespace CoppAddresd.Community.GraphQL;

/// <summary>
/// Autentica las conexiones WebSocket de GraphQL a partir del token
/// enviado en connectionParams (Authorization) del cliente graphql-ws.
/// El token se valida directamente con los mismos parámetros que el esquema
/// JwtBearer (el handler por defecto no lee el header en el contexto
/// WebSocket, devolviendo NoResult).
/// </summary>
public sealed class SubscriptionAuthInterceptor : DefaultSocketSessionInterceptor
{
    private readonly TokenValidationParameters _tokenValidation;
    private readonly JwtSecurityTokenHandler _handler = new();

    public SubscriptionAuthInterceptor(IConfiguration configuration)
    {
        var jwt = configuration.GetSection("Jwt");
        var secret = jwt["Secret"]!;
        var issuer = jwt["Issuer"]!;
        var audiences = jwt.GetSection("ValidAudiences").Get<string[]>() ?? ["app", "erp"];

        _tokenValidation = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudiences = audiences,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    }

    public override async ValueTask<ConnectionStatus> OnConnectAsync(
        ISocketSession session,
        IOperationMessagePayload message,
        CancellationToken cancellationToken)
    {
        string? header = null;
        if (message.Payload is JsonElement json &&
            json.TryGetProperty("Authorization", out var authValue))
        {
            header = authValue.GetString();
        }

        if (string.IsNullOrWhiteSpace(header) ||
            !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionStatus.Reject("No autenticado.");
        }

        var token = header["Bearer ".Length..].Trim();
        var result = await _handler.ValidateTokenAsync(token, _tokenValidation);
        if (!result.IsValid || result.ClaimsIdentity is null)
        {
            return ConnectionStatus.Reject("Token inválido.");
        }

        session.Connection.HttpContext.User = new ClaimsPrincipal(result.ClaimsIdentity);
        return ConnectionStatus.Accept();
    }
}
