using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HotChocolate.AspNetCore;
using HotChocolate.AspNetCore.Subscriptions;
using HotChocolate.AspNetCore.Subscriptions.Protocols;
using Microsoft.IdentityModel.Tokens;
using CoppAddresd.Shared.Security;
using HotChocolate.Execution;

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

        var context = session.Connection.HttpContext;
        context.User = new ClaimsPrincipal(result.ClaimsIdentity);
        context.Items[ErpSessionValidation.TokenKey] = token;
        if (!await HasAccessAsync(session, cancellationToken))
            return ConnectionStatus.Reject("La sesión ERP no está disponible.");
        return ConnectionStatus.Accept();
    }

    public override async ValueTask OnRequestAsync(ISocketSession session, string operationSessionId,
        OperationRequestBuilder requestBuilder, CancellationToken cancellationToken)
    {
        if (!await HasAccessAsync(session, cancellationToken))
            throw new GraphQLException("La sesión ERP fue revocada o no está disponible.");
        await base.OnRequestAsync(session, operationSessionId, requestBuilder, cancellationToken);
    }

    public override async ValueTask<OperationResult> OnResultAsync(ISocketSession session, string operationSessionId,
        OperationResult result, CancellationToken cancellationToken)
    {
        if (!await HasAccessAsync(session, cancellationToken))
            throw new GraphQLException("La sesión ERP fue revocada o no está disponible.");
        return await base.OnResultAsync(session, operationSessionId, result, cancellationToken);
    }

    private static async Task<bool> HasAccessAsync(ISocketSession session, CancellationToken ct)
    {
        var context = session.Connection.HttpContext;
        if (!context.User.HasClaim("aud", "erp")) return true;
        try
        {
            return context.Items[ErpSessionValidation.TokenKey] is string token
                && await context.RequestServices.GetRequiredService<ErpSessionValidation>()
                    .ValidateAsync(context.User, token, ct);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }
}
