using System.Security.Cryptography;
using System.Text;
using CoppAddresd.Gateway.Configuration;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Gateway.Middleware;

/// <summary>
/// Bloquea los endpoints internos (<c>/api/auth/internal/*</c> y <c>/api/v1/internal/*</c>)
/// cuando NO llegan con el header <c>X-Internal-Key</c> correcto.
/// Devuelve <c>404 application/problem+json</c> (no 401/403) para ocultar la existencia
/// del endpoint (REQ-GW-006). La comparación se hace en tiempo constante para evitar
/// timing attacks.
/// </summary>
public sealed class InternalKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly InternalKeySettings _settings;
    private readonly ILogger<InternalKeyMiddleware> _logger;

    public InternalKeyMiddleware(
        RequestDelegate next,
        IOptions<InternalKeySettings> settings,
        ILogger<InternalKeyMiddleware> logger)
    {
        _next = next;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Fail-closed: se protege tanto el prefijo con barra como el path base exacto
        // (sin barra), por si un futuro endpoint se registra en /api/auth/internal o
        // /api/v1/internal sin sub-ruta (REQ-GW-006).
        var isInternal =
            path.Equals("/api/auth/internal", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/auth/internal/", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/v1/internal", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/v1/internal/", StringComparison.OrdinalIgnoreCase);

        if (!isInternal)
        {
            // Path público: el reverse proxy resuelve normalmente.
            await _next(context);
            return;
        }

        // El bloqueo está deshabilitado por configuración: rechazar de todos modos
        // (nunca exponer endpoints internos sin clave).
        if (!_settings.Enabled)
        {
            _logger.LogWarning("Internal deshabilitado pero endpoint interno solicitado: {Path}", path);
            await RejectAsync(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Internal-Key", out var header) ||
            string.IsNullOrEmpty(header.FirstOrDefault()))
        {
            _logger.LogWarning(
                "Intento a endpoint interno sin X-Internal-Key desde {Ip} a {Path}",
                context.Connection.RemoteIpAddress, path);
            await RejectAsync(context);
            return;
        }

        var provided = Encoding.UTF8.GetBytes(header.First()!);
        var expected = Encoding.UTF8.GetBytes(_settings.Key);
        if (provided.Length != expected.Length ||
            !CryptographicOperations.FixedTimeEquals(provided, expected))
        {
            _logger.LogWarning(
                "X-Internal-Key inválida hacia {Path} desde {Ip}",
                path, context.Connection.RemoteIpAddress);
            await RejectAsync(context);
            return;
        }

        // Clave válida: se reenvía al backend con el header intacto.
        await _next(context);
    }

    private static async Task RejectAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        // WriteAsJsonAsync agrega "; charset=utf-8"; REQ-GW-006 exige el tipo
        // exacto application/problem+json, por eso se escribe el body a mano.
        context.Response.ContentType = "application/problem+json";
        const string body =
            "{\"type\":\"about:blank\",\"title\":\"Not Found\",\"status\":404," +
            "\"detail\":\"The requested resource was not found.\"}";
        await context.Response.WriteAsync(body);
    }
}
