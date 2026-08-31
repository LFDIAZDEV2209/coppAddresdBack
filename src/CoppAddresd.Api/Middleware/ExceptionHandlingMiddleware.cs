using System.Text.Json;
using CoppAddresd.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoppAddresd.Api.Middleware;

/// <summary>
/// Middleware global de excepciones de la API principal. Traduce excepciones
/// conocidas a <c>ProblemDetails</c> (RFC 7807) con el status correcto:
/// validación FluentValidation → 400 con errores por propiedad, reglas de
/// negocio → 409, referencias inválidas → 422, no encontrado → 404, violación
/// de unicidad/FK en BD → 409/422. Cualquier excepción inesperada → 500
/// genérico (nunca se exponen stack traces ni mensajes internos).
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // El cliente canceló la petición: no es un error del servidor.
            logger.LogDebug("Petición cancelada por el cliente: {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = 499;
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Operación cancelada: {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = 499;
        }
        catch (ValidationException ex)
        {
            logger.LogWarning(ex, "Validación fallida en {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Validation Error",
                "La solicitud contiene datos inválidos.",
                ex.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(e => e.ErrorMessage).ToArray()));
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning(ex, "Recurso no encontrado en {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblemAsync(
                context,
                StatusCodes.Status404NotFound,
                "Not Found",
                ex.Message);
        }
        catch (ForbiddenException ex)
        {
            logger.LogWarning(ex, "Acceso prohibido en {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblemAsync(
                context,
                StatusCodes.Status403Forbidden,
                "Forbidden",
                ex.Message);
        }
        catch (BusinessRuleViolationException ex)
        {
            logger.LogWarning(ex, "Regla de negocio violada en {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "Conflict",
                ex.Message);
        }
        catch (UnprocessableEntityException ex)
        {
            logger.LogWarning(ex, "Payload semánticamente inválido en {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblemAsync(
                context,
                StatusCodes.Status422UnprocessableEntity,
                "Unprocessable Entity",
                ex.Message);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg)
        {
            // Violaciones de restricción de BD traducidas a HTTP sin exponer SQL.
            if (pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                logger.LogWarning(ex, "Violación de unicidad en {Method} {Path}",
                    context.Request.Method, context.Request.Path);
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status409Conflict,
                    "Conflict",
                    "El registro ya existe o entra en conflicto con otro existente.");
            }
            else if (pg.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                logger.LogWarning(ex, "Violación de llave foránea en {Method} {Path}",
                    context.Request.Method, context.Request.Path);
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status422UnprocessableEntity,
                    "Unprocessable Entity",
                    "La referencia a un registro relacionado no existe.");
            }
            else
            {
                await WriteUnexpectedAsync(context, ex);
            }
        }
        catch (Exception ex)
        {
            await WriteUnexpectedAsync(context, ex);
        }
    }

    /// <summary>
    /// Error inesperado: se loguea completo (con correlation ID) pero el
    /// cliente solo recibe un mensaje genérico, sin stack ni detalles.
    /// </summary>
    private async Task WriteUnexpectedAsync(HttpContext context, Exception ex)
    {
        logger.LogError(ex, "Excepción no controlada en {Method} {Path}",
            context.Request.Method, context.Request.Path);
        await WriteProblemAsync(
            context,
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            "Ocurrió un error interno del servidor. Intenta más tarde.");
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string title,
        string detail,
        Dictionary<string, string[]>? errors = null)
    {
        var correlationId = context.Items.TryGetValue("CorrelationId", out var value)
            ? value?.ToString()
            : null;

        var problem = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.io/{status}",
            ["title"] = title,
            ["status"] = status,
            ["detail"] = detail,
            ["instance"] = context.Request.Path.ToString(),
            ["correlationId"] = correlationId,
        };
        if (errors is not null)
        {
            problem["errors"] = errors;
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json; charset=utf-8";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, JsonOptions));
    }
}
