using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Persistence;
using HotChocolate.Subscriptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CoppAddresd.Community.IntegrationTests;

/// <summary>
/// Dobles y datos de prueba del microservicio de comunidad: fakes en memoria para
/// el sender de tópicos (ITopicEventSender) y helpers de contexto HTTP/claims y de
/// siembra de perfiles, seguimientos y mensajes.
/// </summary>
public static class CommunityTestData
{
    /// <summary>IHttpContextAccessor con el claim de NameIdentifier = userId.</summary>
    public static IHttpContextAccessor HttpAs(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test");
        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
    }

    /// <summary>Crea un perfil activo para el usuario dado.</summary>
    public static async Task<Profile> SeedProfileAsync(
        CommunityDbContext db, Guid userId, string displayName, CancellationToken ct = default)
    {
        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DisplayName = displayName,
            Status = ProfileStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
        db.Profiles.Add(profile);
        await db.SaveChangesAsync(ct);
        return profile;
    }

    /// <summary>Crea una relación de seguimiento follower → following.</summary>
    public static async Task SeedFollowAsync(
        CommunityDbContext db, Guid followerId, Guid followingId, CancellationToken ct = default)
    {
        db.Follows.Add(new Follow
        {
            Id = Guid.NewGuid(),
            FollowerProfileId = followerId,
            FollowingProfileId = followingId,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Inserta un mensaje con fecha explícita (orden determinista).</summary>
    public static async Task SeedMessageAsync(
        CommunityDbContext db, Guid senderId, Guid recipientId, string body,
        DateTime createdAt, CancellationToken ct = default)
    {
        db.Messages.Add(new Message
        {
            Id = Guid.NewGuid(),
            SenderProfileId = senderId,
            RecipientProfileId = recipientId,
            Body = body,
            CreatedAt = createdAt,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Clave de conversación idéntica a la que calculan backend y frontend.</summary>
    public static string ConversationKey(Guid a, Guid b)
        => string.Join(':', new[] { a.ToString(), b.ToString() }.OrderBy(x => x, StringComparer.Ordinal));
}

/// <summary>Sender de tópicos que registra los eventos publicados (sustituye al in-memory real).</summary>
public sealed class RecordingTopicEventSender : ITopicEventSender
{
    public List<(string Topic, object? Message)> Sent { get; } = [];

    public ValueTask SendAsync<TTopic, TMessage>(string topicName, TMessage message, CancellationToken ct = default)
    {
        Sent.Add((topicName, message));
        return ValueTask.CompletedTask;
    }

    public ValueTask SendAsync<TMessage>(string topicName, TMessage message, CancellationToken ct = default)
    {
        Sent.Add((topicName, message));
        return ValueTask.CompletedTask;
    }

    public ValueTask CompleteAsync(string topicName)
        => ValueTask.CompletedTask;
}
