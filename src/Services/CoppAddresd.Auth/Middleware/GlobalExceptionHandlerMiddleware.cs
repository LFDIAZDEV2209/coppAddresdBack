using System.Net;
using System.Text.Json;
using CoppAddresd.Auth.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Auth.Middleware;

/// <summary>
/// Middleware global de excepciones del Auth Service. Traduce excepciones
/// conocidas a ProblemDetails (RFC 7807): errores de negocio/Identity → 400,
/// no autorizado → 401, no encontrado → 404, conflicto → 409, errores de
/// Twilio OTP → status según clasificación (400/429/502/503). Cualquier
/// excepción inesperada → 500 genérico (nunca se expone el mensaje interno).
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Acceso no autorizado en {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized,
                "Unauthorized", "No autorizado.");
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Recurso no encontrado en {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status404NotFound,
                "Not Found", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Operación inválida en {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest,
                "Bad Request", ex.Message);
        }
        catch (TwilioOtpException ex)
        {
            // Errores de Twilio Verify: se clasifican por Kind para decidir el
            // status code y un mensaje seguro para el cliente. Nunca se exponen
            // credenciales, stack trace ni la respuesta cruda del SDK.
            _logger.LogWarning(ex, "Error de Twilio OTP (Kind={Kind}) en {Method} {Path}",
                ex.Kind, context.Request.Method, context.Request.Path);

            var (status, title, detail) = ex.Kind switch
            {
                // Errores del cliente: teléfono/código malformado.
                TwilioOtpErrorKind.InvalidPhone or TwilioOtpErrorKind.InvalidParameter =>
                    (StatusCodes.Status400BadRequest, "Bad Request", ex.Message),

                // Límite de tasa de Twilio.
                TwilioOtpErrorKind.RateLimited =>
                    (StatusCodes.Status429TooManyRequests, "Too Many Requests",
                        "Demasiadas solicitudes de verificación. Intenta más tarde."),

                // Servicio/proveedor no disponible o mal configurado en el servidor.
                TwilioOtpErrorKind.ProviderUnavailable
                    or TwilioOtpErrorKind.Disabled
                    or TwilioOtpErrorKind.InvalidConfiguration =>
                    (StatusCodes.Status503ServiceUnavailable, "Service Unavailable",
                        "La verificación por SMS no está disponible. Intenta más tarde."),

                // Error genérico del proveedor.
                _ => (StatusCodes.Status502BadGateway, "Bad Gateway",
                    "El proveedor de verificación no pudo completar la operación. Intenta más tarde."),
            };

            await WriteProblemAsync(context, status, title, detail);
        }
        catch (OtpProtectionException ex)
        {
            // Bloqueo del motor de protección OTP: responde 429 con el mismo
            // shape que el rate limiter global ("message"), sin exponer la
            // razón interna (IP/teléfono/documento/lockout) ni la existencia
            // del paciente. Retry-After solo si el motor lo informó.
            _logger.LogWarning("Operación OTP bloqueada (Reason={Reason}) en {Method} {Path}",
                ex.Reason, context.Request.Method, context.Request.Path);

            if (ex.RetryAfterSeconds.HasValue)
            {
                context.Response.Headers["Retry-After"] = ex.RetryAfterSeconds.Value.ToString();
            }

            await WriteRateLimitMessageAsync(context, "Demasiadas peticiones. Intenta más tarde.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción no controlada en {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "Ocurrió un error interno del servidor. Intenta más tarde.");
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string title,
        string detail)
    {
        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path.ToString(),
        };

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }));
    }

    /// <summary>
    /// Responde 429 con el mismo shape que usa el rate limiter global
    /// (<c>{"message":"..."}</c>), para que el frontend trate por igual los
    /// bloqueos del motor OTP y los del rate limiter.
    /// </summary>
    private static async Task WriteRateLimitMessageAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { message }));
    }
}
