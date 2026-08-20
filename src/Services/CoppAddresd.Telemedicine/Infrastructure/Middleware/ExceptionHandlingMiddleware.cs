using System.Diagnostics;
using System.Text.Json;
using CoppAddresd.Telemedicine.Application.Exceptions;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Telemedicine.Infrastructure.Middleware;

/// <summary>
/// Middleware global de errores (RFC 7807 / ProblemDetails). Traduce excepciones
/// conocidas del dominio/aplicación a su status HTTP y devuelve 500 genérico
/// (sin detalles internos) para el resto. La cancelación del cliente no se
/// loguea como error.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException)
        {
            // Cliente canceló la petición: no es un error del servidor.
            context.Response.StatusCode = 499;
        }
        catch (RequestValidationException ex)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Bad Request",
                ex.Message,
                errors: ex.Failures.ToDictionary(
                    f => f.PropertyName,
                    f => (object)f.ErrorMessage));
        }
        catch (Exception ex) when (MapException(ex) is { } mapped)
        {
            await WriteProblemAsync(context, mapped.Status, mapped.Title, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error no controlado en {Path}", context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                "Internal Server Error", "Ocurrió un error inesperado.");
        }
    }

    private static (int Status, string Title)? MapException(Exception ex) => ex switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
        BusinessRuleViolationException => (StatusCodes.Status409Conflict, "Conflict"),
        DomainValidationException => (StatusCodes.Status400BadRequest, "Bad Request"),
        RequestValidationException => (StatusCodes.Status400BadRequest, "Bad Request"),
        _ => null
    };

    private static async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string title,
        string detail,
        IReadOnlyDictionary<string, object>? errors = null)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["correlationId"] = traceId;
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, JsonSerializerOptions.Web));
    }
}
