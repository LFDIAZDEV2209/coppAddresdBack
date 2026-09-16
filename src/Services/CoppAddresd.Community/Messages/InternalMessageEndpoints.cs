using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Persistence;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.Messages;

/// <summary>
/// Endpoint interno (servicio a servicio) para que el backend ERP entregue un
/// mensaje directo del perfil de sistema a un paciente de la comunidad.
/// Se autentica con el header <c>X-Internal-Key</c> (nunca con JWT de usuario),
/// por lo que NO debe exponerse fuera de la red interna.
/// </summary>
public static class InternalMessageEndpoints
{
    private const string InternalKeyHeader = "X-Internal-Key";

    /// <summary>Cuerpo del mensaje interno de entrega directa.</summary>
    public sealed record InternalDirectMessageRequest(
        Guid? RecipientProfileId,
        Guid? PatientUserId,
        string Body,
        Guid? ActorUserId,
        string? DisplayName = null
    );

    public static IEndpointRouteBuilder MapInternalMessageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/internal");

        // Entrega de un mensaje directo (alertas de tests de salud del ERP hacia
        // la app del paciente). Sin autorización JWT: la clave interna es la auth.
        group
            .MapPost("/messages/direct", SendDirectMessageAsync)
            .WithName("InternalSendDirectMessage")
            .DisableAntiforgery();

        return app;
    }

    private static async Task<IResult> SendDirectMessageAsync(
        InternalDirectMessageRequest request,
        HttpRequest http,
        IConfiguration configuration,
        CommunityDbContext db,
        ITopicEventSender sender,
        CancellationToken ct)
    {
        var expectedKey =
            configuration["Community:InternalApiKey"]
            ?? Environment.GetEnvironmentVariable("COMMUNITY_INTERNAL_API_KEY");

        // Si la clave no está configurada, el endpoint queda deshabilitado (503)
        // en lugar de quedar abierto sin autenticación.
        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            return Results.Problem(
                title: "Endpoint interno deshabilitado",
                detail: "Falta configurar Community:InternalApiKey.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (
            !http.Headers.TryGetValue(InternalKeyHeader, out var provided)
            || !string.Equals(provided.ToString(), expectedKey, StringComparison.Ordinal)
        )
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return Results.BadRequest(new { error = "El cuerpo del mensaje es obligatorio." });
        }

        var systemProfile = await GetSystemProfileAsync(db, ct);

        var recipient = request.RecipientProfileId is { } profileId
            ? await db.Profiles.FirstOrDefaultAsync(p => p.Id == profileId, ct)
            : request.PatientUserId is { } userId
                ? await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, ct)
                : null;

        // Auto-provisión del perfil: un paciente con cuenta en Auth puede no
        // haber abierto nunca la app, y en ese caso no tiene perfil todavía.
        // Se crea activo (mismo criterio que la auto-provisión de `Me`), de modo
        // que la notificación clínica no se pierda por un detalle de onboarding.
        if (recipient is null)
        {
            if (request.PatientUserId is not { } newUserId)
            {
                return Results.NotFound(
                    new { error = "El destinatario no existe en la comunidad." });
            }

            recipient = await EnsureRecipientProfileAsync(db, newUserId, request.DisplayName, ct);
        }

        if (recipient.Status != ProfileStatus.Active)
        {
            return Results.BadRequest(new { error = "El destinatario no está activo." });
        }

        // Perfil que "firma" como disparador: el del usuario ERP si se conoce,
        // de lo contrario el perfil de sistema.
        var triggeredProfileId = systemProfile.Id;
        if (request.ActorUserId is { } actorUserId)
        {
            var actor = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == actorUserId, ct);
            if (actor is not null)
            {
                triggeredProfileId = actor.Id;
            }
        }

        var now = DateTime.UtcNow;
        var message = new Message
        {
            Id = Guid.NewGuid(),
            SenderProfileId = systemProfile.Id,
            RecipientProfileId = recipient.Id,
            Body = request.Body.Trim(),
            CreatedAt = now,
            TriggeredByProfileId = triggeredProfileId,
        };

        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);

        var feedEvent = new FeedEvent
        {
            Id = Guid.NewGuid(),
            ProfileId = systemProfile.Id,
            Kind = FeedEventKind.Mensaje,
            Body = $"Mensaje enviado a {recipient.DisplayName}",
            CreatedAt = now,
        };

        db.FeedEvents.Add(feedEvent);
        await db.SaveChangesAsync(ct);

        await sender.SendAsync("feed_event_added", feedEvent, ct);

        return Results.Ok(new { messageId = message.Id, recipientProfileId = recipient.Id });
    }

    private static async Task<Profile> GetSystemProfileAsync(
        CommunityDbContext db,
        CancellationToken ct)
    {
        return await db.Profiles.FirstOrDefaultAsync(p => p.IsSystem, ct)
            ?? throw new InvalidOperationException(
                "No existe el perfil de sistema de la comunidad (Profile.IsSystem).");
    }

    /// <summary>
    /// Crea el perfil de un usuario que aún no lo tiene (paciente con cuenta pero
    /// sin primer acceso a la app). Idempotente frente a carreras: si otro request
    /// lo creó en paralelo, se relee el existente.
    /// </summary>
    private static async Task<Profile> EnsureRecipientProfileAsync(
        CommunityDbContext db,
        Guid userId,
        string? displayName,
        CancellationToken ct)
    {
        var existing = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (existing is not null)
        {
            return existing;
        }

        var created = new Profile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? "Miembro ANTARES"
                : displayName!.Trim(),
            Status = ProfileStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };

        db.Profiles.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }
}
