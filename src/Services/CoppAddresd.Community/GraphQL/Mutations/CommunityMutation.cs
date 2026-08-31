using CoppAddresd.Application.Interfaces;
using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.GraphQL.Types;
using CoppAddresd.Community.Persistence;
using CoppAddresd.Community.Security;
using CoppAddresd.Community.Storage;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Subscriptions;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.GraphQL.Mutations;

[Authorize]
public sealed class CommunityMutation
{
    // --- Perfiles ---

    public async Task<Profile> UpdateProfile(
        string displayName,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        string? bio,
        string? avatarKey,
        string? coverKey,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);

        if (!string.IsNullOrWhiteSpace(avatarKey) && !PostStorageEndpoints.IsAvatarKey(avatarKey))
            throw new GraphQLException("La foto de perfil no es válida.");
        if (!string.IsNullOrWhiteSpace(coverKey) && !PostStorageEndpoints.IsCoverKey(coverKey))
            throw new GraphQLException("La portada no es válida.");

        profile.DisplayName = displayName;
        profile.Bio = bio;
        profile.AvatarKey = string.IsNullOrWhiteSpace(avatarKey) ? null : avatarKey;
        profile.CoverKey = string.IsNullOrWhiteSpace(coverKey) ? null : coverKey;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return profile;
    }

    /// <summary>
    /// Crea la información de subida de la foto de perfil o de portada:
    /// clave, URL de subida y URL de lectura firmada (S3 presigned o proxy local).
    /// </summary>
    public async Task<PostImageUploadInfo> CreateProfileImageUploadInfo(
        string kind,
        string fileName,
        string contentType,
        [Service] IObjectStorageService storage,
        [Service] IConfiguration config,
        [Service] StorageSignatureService signer,
        CancellationToken ct)
    {
        var isCover = kind?.Equals("COVER", StringComparison.OrdinalIgnoreCase) == true;
        var prefix = isCover ? PostStorageEndpoints.CoverPrefix : PostStorageEndpoints.AvatarPrefix;

        var normalizedContentType = contentType.Split(';')[0].Trim().ToLowerInvariant();
        if (!PostStorageEndpoints.IsAllowedImageContentType(normalizedContentType)
            || normalizedContentType.StartsWith("video/"))
            throw new GraphQLException("La foto de perfil/portada debe ser una imagen (JPG, PNG, WEBP, GIF, HEIC).");

        var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        if (!PostStorageEndpoints.IsAllowedImageExtension(extension)
            || extension is ".mp4" or ".webm")
            throw new GraphQLException("Formato no permitido para la foto.");

        var key = $"{prefix}{Guid.NewGuid():N}{extension}";
        var publicBase = config["Storage:PublicBaseUrl"] ?? string.Empty;

        if (storage.IsCloudStorage)
        {
            var cloudUploadUrl = await storage.GetPreSignedUploadUrlAsync(
                key, normalizedContentType, TimeSpan.FromMinutes(15), publicBase, ct);
            var cloudReadUrl = await storage.GetPreSignedUrlAsync(key, TimeSpan.FromHours(1), ct);
            return new PostImageUploadInfo(key, cloudUploadUrl, cloudReadUrl);
        }

        var localUploadUrl = $"{publicBase}/storage/{key}";
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var localReadUrl =
            $"{publicBase}/storage/{key}?sig={signer.Sign(key, expiresAt)}&exp={expiresAt.ToUnixTimeSeconds()}";

        return new PostImageUploadInfo(key, localUploadUrl, localReadUrl);
    }

    // --- Publicaciones ---

    public async Task<Post> CreatePost(
        string body,
        PostType? type,
        PostDestination? destination,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        string? imageKey,
        CancellationToken ct,
        bool pinned = false)
    {
        var profile = await RequireProfileAsync(db, http, ct);

        // La clave de imagen es opcional; si llega, debe ser una clave válida
        // del prefijo reservado para publicaciones (la subió este servicio).
        if (!string.IsNullOrWhiteSpace(imageKey) && !PostStorageEndpoints.IsValidPostImageKey(imageKey))
            throw new GraphQLException("La imagen adjunta no es válida.");

        var now = DateTime.UtcNow;

        // Derive post type: explicit type wins; otherwise infer from imageKey extension.
        var effectiveType = type ?? (!string.IsNullOrWhiteSpace(imageKey)
            ? System.IO.Path.GetExtension(imageKey).ToLowerInvariant() is ".mp4" or ".webm"
                ? PostType.Video
                : PostType.Imagen
            : PostType.Texto);

        int pinnedOrder = 0;
        if (pinned)
        {
            var maxOrder = await db.Posts
                .Where(p => p.Pinned)
                .Select(p => (int?)p.PinnedOrder)
                .MaxAsync(ct) ?? 0;
            pinnedOrder = maxOrder + 1;
        }

        var post = new Post
        {
            Id = Guid.NewGuid(),
            ProfileId = profile.Id,
            Body = body,
            ImageKey = string.IsNullOrWhiteSpace(imageKey) ? null : imageKey,
            Type = effectiveType,
            Destination = destination ?? PostDestination.TodasLasComunidades,
            Pinned = pinned,
            PinnedOrder = pinnedOrder,
            CreatedAt = now,
        };
        db.Posts.Add(post);

        // Actualiza señales de actividad y recalcula la racha del perfil.
        profile.LastPostAt = DateTimeOffset.UtcNow;
        profile.LastActiveAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        await RecomputeStreakAsync(db, profile, ct);

        // Emite un evento de feed en vivo a partir del tipo de publicación.
        var feedEvent = new FeedEvent
        {
            Id = Guid.NewGuid(),
            ProfileId = profile.Id,
            Kind = MapPostTypeToFeedEvent(post.Type!.Value),
            Body = body.Length > 500 ? body[..500] : body,
            CreatedAt = now,
        };
        db.FeedEvents.Add(feedEvent);
        await db.SaveChangesAsync(ct);

        await db.Entry(post).Reference(p => p.Profile).LoadAsync(ct);
        await sender.SendAsync("post_added", post);
        await sender.SendAsync("feed_event_added", feedEvent);
        return post;
    }

    [Authorize(Policy = "Community.Manage")]
    public async Task<Post> CreateAnnouncement(
        string body,
        PostType? type,
        PostDestination? destination,
        [Service] CommunityDbContext db,
        [Service] ITopicEventSender sender,
        CancellationToken ct,
        bool pinned = false)
    {
        body = body.Trim();
        if (body.Length == 0) throw new GraphQLException("Escribe el contenido de la publicación.");
        var systemProfile = await GetSystemProfileAsync(db, ct);
        var now = DateTime.UtcNow;

        int pinnedOrder = 0;
        if (pinned)
        {
            var maxOrder = await db.Posts
                .Where(p => p.Pinned)
                .Select(p => (int?)p.PinnedOrder)
                .MaxAsync(ct) ?? 0;
            pinnedOrder = maxOrder + 1;
        }

        var post = new Post
        {
            Id = Guid.NewGuid(),
            ProfileId = systemProfile.Id,
            Body = body,
            Type = type ?? PostType.Texto,
            Destination = destination ?? PostDestination.TodasLasComunidades,
            Pinned = pinned,
            PinnedOrder = pinnedOrder,
            CreatedAt = now,
        };
        db.Posts.Add(post);

        var feedEvent = new FeedEvent
        {
            Id = Guid.NewGuid(),
            ProfileId = systemProfile.Id,
            Kind = MapPostTypeToFeedEvent(post.Type!.Value),
            Body = body.Length > 500 ? body[..500] : body,
            CreatedAt = now,
        };
        db.FeedEvents.Add(feedEvent);
        await db.SaveChangesAsync(ct);

        await db.Entry(post).Reference(p => p.Profile).LoadAsync(ct);
        await sender.SendAsync("post_added", post);
        await sender.SendAsync("feed_event_added", feedEvent);
        return post;
    }

    public async Task<Post?> ViewPost(
        Guid id,
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, ct)
            ?? throw new GraphQLException("No se encontró la publicación.");
        post.ViewCount += 1;
        await db.SaveChangesAsync(ct);        return post;
    }

    // --- Encuestas (poll posts) ---

    /// <summary>
    /// Crea una publicación de encuesta: el body del post es la pregunta y las
    /// opciones se guardan en la tabla de poll_options (2 a 4, únicas y no vacías).
    /// Publica el evento post_added para que el feed de la comunidad se entere.
    /// </summary>
    public async Task<Post> CreatePollPost(
        string question,
        List<string> options,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);

        question = question.Trim();
        if (question.Length < 3 || question.Length > 300)
            throw new GraphQLException("La pregunta debe tener entre 3 y 300 caracteres.");

        var normalized = (options ?? [])
            .Select(o => o.Trim())
            .Where(o => o.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalized.Count is < 2 or > 4)
            throw new GraphQLException("La encuesta necesita entre 2 y 4 opciones.");
        if (normalized.Any(o => o.Length > 100))
            throw new GraphQLException("Cada opción debe tener máximo 100 caracteres.");

        var now = DateTime.UtcNow;
        var post = new Post
        {
            Id = Guid.NewGuid(),
            ProfileId = profile.Id,
            Body = question,
            Type = PostType.Encuesta,
            Destination = PostDestination.TodasLasComunidades,
            Pinned = false,
            CreatedAt = now,
        };
        var poll = new Poll
        {
            Id = Guid.NewGuid(),
            PostId = post.Id,
            CreatedAt = now,
        };
        post.Poll = poll;
        for (var i = 0; i < normalized.Count; i++)
        {
            poll.Options.Add(new PollOption
            {
                Id = Guid.NewGuid(),
                PollId = poll.Id,
                Text = normalized[i],
                Position = i,
            });
        }

        db.Posts.Add(post);

        // Emitir evento de feed coherente con el tipo de publicación (Encuesta).
        var feedEvent = new FeedEvent
        {
            Id = Guid.NewGuid(),
            ProfileId = profile.Id,
            Kind = FeedEventKind.Publicacion,
            Body = question.Length > 500 ? question[..500] : question,
            CreatedAt = now,
        };
        db.FeedEvents.Add(feedEvent);

        await db.SaveChangesAsync(ct);
        await db.Entry(post).Reference(p => p.Profile).LoadAsync(ct);
        await sender.SendAsync("post_added", post);
        await sender.SendAsync("feed_event_added", feedEvent);

        return post;
    }

    /// <summary>
    /// Registra el voto del perfil actual a una opción (un voto por encuesta).
    /// Devuelve el post completo (con la encuesta y sus votos) para que el
    /// cliente pueda pintar los resultados al instante.
    /// </summary>
    public async Task<Post> VotePoll(
        Guid optionId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var option = await db.PollOptions
            .Include(o => o.Poll)
            .FirstOrDefaultAsync(o => o.Id == optionId, ct)
            ?? throw new GraphQLException("No se encontró la opción de la encuesta.");

        var alreadyVoted = await db.PollVotes.AnyAsync(v =>
            v.OptionId == option.Id && v.ProfileId == profile.Id, ct);
        if (alreadyVoted)
            throw new GraphQLException("Ya votaste esta encuesta.");

        db.PollVotes.Add(new PollVote
        {
            Id = Guid.NewGuid(),
            OptionId = option.Id,
            ProfileId = profile.Id,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        var post = await db.Posts
            .Include(p => p.Profile)
            .Include(p => p.Likes)
            .Include(p => p.Poll).ThenInclude(p => p.Options).ThenInclude(o => o.Votes)
            .Include(p => p.Comments).ThenInclude(c => c.Profile)
            .Include(p => p.Comments).ThenInclude(c => c.Likes)
            .Include(p => p.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.Profile)
            .Include(p => p.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.Likes)
            .FirstOrDefaultAsync(
                p => p.Poll!.Options.Any(o => o.Id == optionId) && p.DeletedAt == null, ct)
            ?? throw new GraphQLException("No se encontró la encuesta.");

        return post;
    }

    /// <summary>
    /// Crea la información de subida de la imagen de una publicación:
    /// clave de almacenamiento, URL de subida y URL de lectura. El cliente hace
    /// un PUT binario a <c>uploadUrl</c> (con el token si el proveedor es Local;
    /// con S3 la URL ya es una presigned PUT). Devuelve <see cref="PostImageUploadInfo"/>.
    /// </summary>
    public async Task<PostImageUploadInfo> CreatePostImageUploadInfo(
        string fileName,
        string contentType,
        [Service] IObjectStorageService storage,
        [Service] IConfiguration config,
        [Service] StorageSignatureService signer,
        CancellationToken ct)
    {
        var normalizedContentType = contentType.Split(';')[0].Trim().ToLowerInvariant();
        if (!PostStorageEndpoints.IsAllowedImageContentType(normalizedContentType))
            throw new GraphQLException($"Tipo de adjunto no permitido: '{contentType}'.");

        var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        if (!PostStorageEndpoints.IsAllowedImageExtension(extension))
            throw new GraphQLException("El adjunto debe ser una imagen (JPG, PNG, WEBP, GIF, HEIC) o un video (MP4, WEBM).");

        var key = $"{PostStorageEndpoints.KeyPrefix}{Guid.NewGuid():N}{extension}";
        var publicBase = config["Storage:PublicBaseUrl"] ?? string.Empty;

        if (storage.IsCloudStorage)
        {
            var cloudUploadUrl = await storage.GetPreSignedUploadUrlAsync(
                key, normalizedContentType, TimeSpan.FromMinutes(15), publicBase, ct);
            var cloudReadUrl = await storage.GetPreSignedUrlAsync(key, TimeSpan.FromHours(1), ct);
            return new PostImageUploadInfo(key, cloudUploadUrl, cloudReadUrl);
        }

        // Proveedor Local: el proxy del propio servicio (con Bearer en la subida
        // y URL firmada en la lectura, para que el <img> no requiera headers).
        var localUploadUrl = $"{publicBase}/storage/{key}";
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var localReadUrl =
            $"{publicBase}/storage/{key}?sig={signer.Sign(key, expiresAt)}&exp={expiresAt.ToUnixTimeSeconds()}";

        return new PostImageUploadInfo(key, localUploadUrl, localReadUrl);
    }

    public async Task<Post?> DeletePost(
        Guid id,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == id && p.ProfileId == profile.Id, ct)
            ?? throw new GraphQLException("No encontraste esta publicación.");
        post.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return post;
    }

    // --- Likes ---

    public async Task<Post?> LikePost(
        Guid postId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var exists = await db.Likes.AnyAsync(l => l.PostId == postId && l.ProfileId == profile.Id, ct);
        if (!exists)
        {
            db.Likes.Add(new Like { Id = Guid.NewGuid(), PostId = postId, ProfileId = profile.Id });
            await db.SaveChangesAsync(ct);
        }
        return await db.Posts.FirstOrDefaultAsync(p => p.Id == postId, ct);
    }

    public async Task<Post?> UnlikePost(
        Guid postId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var like = await db.Likes.FirstOrDefaultAsync(l => l.PostId == postId && l.ProfileId == profile.Id, ct);
        if (like is not null)
        {
            db.Likes.Remove(like);
            await db.SaveChangesAsync(ct);
        }
        return await db.Posts.FirstOrDefaultAsync(p => p.Id == postId, ct);
    }

    // --- Likes (comentarios) ---

    public async Task<Comment?> LikeComment(
        Guid commentId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);

        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId && c.DeletedAt == null, ct)
            ?? throw new GraphQLException("No se encontró el comentario.");

        var exists = await db.Likes.AnyAsync(l => l.CommentId == commentId && l.ProfileId == profile.Id, ct);
        if (!exists)
        {
            db.Likes.Add(new Like { Id = Guid.NewGuid(), CommentId = commentId, ProfileId = profile.Id });
            await db.SaveChangesAsync(ct);
        }

        return await db.Comments
            .Include(c => c.Profile)
            .Include(c => c.Likes)
            .FirstOrDefaultAsync(c => c.Id == commentId, ct);
    }

    public async Task<Comment?> UnlikeComment(
        Guid commentId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);

        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId && c.DeletedAt == null, ct)
            ?? throw new GraphQLException("No se encontró el comentario.");

        var like = await db.Likes.FirstOrDefaultAsync(l => l.CommentId == commentId && l.ProfileId == profile.Id, ct);
        if (like is not null)
        {
            db.Likes.Remove(like);
            await db.SaveChangesAsync(ct);
        }

        return await db.Comments
            .Include(c => c.Profile)
            .Include(c => c.Likes)
            .FirstOrDefaultAsync(c => c.Id == commentId, ct);
    }

    // --- Reportes (comentarios) ---

    /// <summary>
    /// Reporta un comentario. Cualquier usuario autenticado puede reportar.
    /// Valida que el comentario exista y no esté eliminado.
    /// </summary>
    public async Task<CommentReport> ReportComment(
        Guid commentId,
        string reason,
        string? details,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);

        // Validar motivo: no vacío, 1..100 caracteres.
        reason = reason?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(reason) || reason.Length > 100)
            throw new GraphQLException("El motivo del reporte debe tener entre 1 y 100 caracteres.");

        // Validar detalles: 0..500 caracteres.
        details = details?.Trim();
        if (!string.IsNullOrEmpty(details) && details.Length > 500)
            throw new GraphQLException("Los detalles del reporte no pueden superar los 500 caracteres.");

        // El comentario debe existir y no estar eliminado.
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId && c.DeletedAt == null, ct)
            ?? throw new GraphQLException("No se encontró el comentario.");

        var report = new CommentReport
        {
            Id = Guid.NewGuid(),
            CommentId = commentId,
            ReportedByProfileId = profile.Id,
            Reason = reason,
            Details = details,
            CreatedAt = DateTime.UtcNow,
        };

        db.CommentReports.Add(report);
        await db.SaveChangesAsync(ct);

        // Cargar navegaciones para la respuesta.
        await db.Entry(report).Reference(r => r.Comment).LoadAsync(ct);
        await db.Entry(report).Reference(r => r.ReportedBy).LoadAsync(ct);

        return report;
    }

    /// <summary>
    /// Resuelve (elimina) un reporte de comentario. Solo moderadores.
    /// </summary>
    [Authorize(Policy = "CommunityModerator")]
    public async Task<bool> ResolveCommentReport(
        Guid reportId,
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var report = await db.CommentReports.FirstOrDefaultAsync(r => r.Id == reportId, ct)
            ?? throw new GraphQLException("No se encontró el reporte.");
        db.CommentReports.Remove(report);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // --- Reportes ---

    /// <summary>
    /// Reporta una publicación. Cualquier usuario autenticado puede reportar.
    /// Valida que la publicación exista y no esté eliminada.
    /// </summary>
    public async Task<PostReport> ReportPost(
        Guid postId,
        string reason,
        string? details,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);

        // Validar motivo: no vacío, 1..100 caracteres.
        reason = reason?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(reason) || reason.Length > 100)
            throw new GraphQLException("El motivo del reporte debe tener entre 1 y 100 caracteres.");

        // Validar detalles: 0..500 caracteres.
        details = details?.Trim();
        if (!string.IsNullOrEmpty(details) && details.Length > 500)
            throw new GraphQLException("Los detalles del reporte no pueden superar los 500 caracteres.");

        // La publicación debe existir y no estar eliminada.
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == postId && p.DeletedAt == null, ct)
            ?? throw new GraphQLException("No se encontró la publicación.");

        var report = new PostReport
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            ReportedByProfileId = profile.Id,
            Reason = reason,
            Details = details,
            CreatedAt = DateTime.UtcNow,
        };

        db.PostReports.Add(report);
        await db.SaveChangesAsync(ct);

        // Cargar navegaciones para la respuesta.
        await db.Entry(report).Reference(r => r.Post).LoadAsync(ct);
        await db.Entry(report).Reference(r => r.ReportedBy).LoadAsync(ct);

        return report;
    }

    // --- Comentarios ---

    public async Task<Comment> AddComment(
        Guid postId,
        string body,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            ProfileId = profile.Id,
            Body = body,
            CreatedAt = DateTime.UtcNow,
        };
        db.Comments.Add(comment);
        profile.LastActiveAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        // Evento de feed para comentarios (actualiza el feed en vivo)
        var feedEvent = new FeedEvent
        {
            Id = Guid.NewGuid(),
            ProfileId = profile.Id,
            Kind = FeedEventKind.Comentario,
            Body = body.Length > 500 ? body[..500] : body,
            CreatedAt = comment.CreatedAt,
        };
        db.FeedEvents.Add(feedEvent);
        await db.SaveChangesAsync(ct);

        await db.Entry(comment).Reference(c => c.Profile).LoadAsync(ct);
        await sender.SendAsync("comment_added", comment);
        await sender.SendAsync("feed_event_added", feedEvent);
        return comment;
    }

    public async Task<Comment> ReplyToComment(
        Guid commentId,
        string body,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var parent = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId, ct)
            ?? throw new GraphQLException("No encontraste el comentario.");
        var reply = new Comment
        {
            Id = Guid.NewGuid(),
            PostId = parent.PostId,
            ProfileId = profile.Id,
            ParentCommentId = parent.ParentCommentId ?? parent.Id,
            Body = body,
            CreatedAt = DateTime.UtcNow,
        };
        db.Comments.Add(reply);
        profile.LastActiveAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var feedEvent = new FeedEvent
        {
            Id = Guid.NewGuid(),
            ProfileId = profile.Id,
            Kind = FeedEventKind.Comentario,
            Body = body.Length > 500 ? body[..500] : body,
            CreatedAt = reply.CreatedAt,
        };
        db.FeedEvents.Add(feedEvent);
        await db.SaveChangesAsync(ct);

        await db.Entry(reply).Reference(c => c.Profile).LoadAsync(ct);
        await sender.SendAsync("comment_added", reply);
        await sender.SendAsync("feed_event_added", feedEvent);
        return reply;
    }

    public async Task<Comment?> DeleteComment(
        Guid id,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == id && c.ProfileId == profile.Id, ct)
            ?? throw new GraphQLException("No encontraste el comentario.");
        comment.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return comment;
    }

    // --- Moderación (admin) ---

    [Authorize(Policy = "CommunityModerator")]
    public async Task<Profile?> BanProfile(
        Guid id,
        string? reason,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new GraphQLException("No se encontró el perfil.");
        profile.Status = ProfileStatus.Banned;
        profile.BannedBy = CommunityQuery.CurrentUserId(http);
        profile.BannedAt = DateTime.UtcNow;
        profile.BanReason = reason;
        await db.SaveChangesAsync(ct);
        return profile;
    }

    [Authorize(Policy = "CommunityModerator")]
    public async Task<Profile?> UnbanProfile(
        Guid id,
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new GraphQLException("No se encontró el perfil.");
        profile.Status = ProfileStatus.Active;
        profile.BannedBy = null;
        profile.BannedAt = null;
        profile.BanReason = null;
        await db.SaveChangesAsync(ct);
        return profile;
    }

    [Authorize(Policy = "CommunityModerator")]
    public async Task<Post?> PinPost(
        Guid id,
        bool pinned,
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new GraphQLException("No se encontró la publicación.");
        post.Pinned = pinned;
        if (pinned && post.PinnedOrder == 0)
        {
            // Place newly pinned post at the end (max order + 1).
            var maxOrder = await db.Posts
                .Where(p => p.Pinned && p.Id != id)
                .Select(p => (int?)p.PinnedOrder)
                .MaxAsync(ct) ?? 0;
            post.PinnedOrder = maxOrder + 1;
        }
        await db.SaveChangesAsync(ct);
        return post;
    }

    /// <summary>
    /// Reorders pinned posts by assigning PinnedOrder values (0..n-1)
    /// matching the order of the provided IDs. All IDs must belong to
    /// currently pinned posts. Returns the reordered posts.
    /// </summary>
    [Authorize(Policy = "CommunityModerator")]
    public async Task<List<Post>> ReorderPinnedPosts(
        List<Guid> orderedIds,
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        if (orderedIds == null || orderedIds.Count == 0)
            throw new GraphQLException("Se requiere al menos una publicación.");

        var pinnedPosts = await db.Posts
            .Where(p => p.Pinned && !p.DeletedAt.HasValue)
            .ToListAsync(ct);

        var pinnedDict = pinnedPosts.ToDictionary(p => p.Id);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            if (!pinnedDict.TryGetValue(orderedIds[i], out var post))
                throw new GraphQLException(
                    $"El post {orderedIds[i]} no está fijado o no existe.");
            post.PinnedOrder = i;
        }

        await db.SaveChangesAsync(ct);

        // Return pinned posts in the new order.
        return orderedIds.Select(id => pinnedDict[id]).ToList();
    }

    [Authorize(Policy = "CommunityModerator")]
    public async Task<Post?> ModerateDeletePost(
        Guid id,
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new GraphQLException("No se encontró la publicación.");
        post.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return post;
    }

    /// <summary>
    /// Soft-delete moderado de un comentario. Solo moderadores.
    /// Establece DeletedAt con la fecha actual.
    /// </summary>
    [Authorize(Policy = "CommunityModerator")]
    public async Task<Comment?> ModerateDeleteComment(
        Guid commentId,
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId, ct)
            ?? throw new GraphQLException("No se encontró el comentario.");
        comment.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return comment;
    }

    /// <summary>
    /// Resuelve (elimina) un reporte. Solo moderadores.
    /// </summary>
    [Authorize(Policy = "CommunityModerator")]
    public async Task<bool> ResolveReport(
        Guid reportId,
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var report = await db.PostReports.FirstOrDefaultAsync(r => r.Id == reportId, ct)
            ?? throw new GraphQLException("No se encontró el reporte.");
        db.PostReports.Remove(report);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // --- Gamificación (gestión) ---

    /// <summary>
    /// Otorga XP a un perfil concreto: crea una XpEntry, incrementa el XP total,
    /// registra una Recognición y emite un evento de feed (racha/hito).
    /// Requiere el permiso Community.Manage.
    /// </summary>
    [Authorize(Policy = "Community.Manage")]
    public async Task<List<Profile>> AwardXp(
        Guid profileId,
        int amount,
        string? reason,
        [Service] CommunityDbContext db,
        [Service] ITopicEventSender sender,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        if (amount <= 0) throw new GraphQLException("La cantidad de XP debe ser mayor que cero.");
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == profileId, ct)
            ?? throw new GraphQLException("No se encontró el perfil.");

        var adminProfileId = CommunityQuery.CurrentUserId(http);
        var adminProfile = adminProfileId.HasValue
            ? await db.Profiles.FirstOrDefaultAsync(p => p.UserId == adminProfileId, ct)
            : null;

        db.XpEntries.Add(new XpEntry
        {
            Id = Guid.NewGuid(),
            ProfileId = profile.Id,
            Amount = amount,
            Reason = reason,
            CreatedAt = DateTime.UtcNow,
        });
        profile.XpTotal += amount;
        profile.LastActiveAt = DateTimeOffset.UtcNow;

        // Registrar reconocimiento por el otorgamiento de XP.
        db.Recognitions.Add(new Recognition
        {
            Id = Guid.NewGuid(),
            ProfileId = profile.Id,
            TypeLabel = reason ?? "Puntos XP",
            Xp = amount,
            Status = RecognitionStatus.Sent,
            CreatedAt = DateTimeOffset.UtcNow,
            TriggeredByProfileId = adminProfile?.Id,
        });

        await db.SaveChangesAsync(ct);

        var feedEvent = new FeedEvent
        {
            Id = Guid.NewGuid(),
            ProfileId = profile.Id,
            Kind = FeedEventKind.Racha,
            Body = reason,
            CreatedAt = DateTime.UtcNow,
        };
        db.FeedEvents.Add(feedEvent);
        await db.SaveChangesAsync(ct);
        await sender.SendAsync("feed_event_added", feedEvent);

        return [profile];
    }

    /// <summary>
    /// Otorga XP a todos los perfiles activos (broadcast). Registra un Recognition
    /// por perfil afectado. Requiere Community.Manage.
    /// </summary>
    [Authorize(Policy = "Community.Manage")]
    public async Task<List<Profile>> AwardXpToAll(
        int amount,
        string? reason,
        [Service] CommunityDbContext db,
        [Service] ITopicEventSender sender,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        if (amount <= 0) throw new GraphQLException("La cantidad de XP debe ser mayor que cero.");
        var adminProfileId = CommunityQuery.CurrentUserId(http);
        // El broadcast nunca toca al perfil sistema (Equipo ANTARES) ni al propio admin.
        var profiles = await db.Profiles
            .Where(p => p.Status == ProfileStatus.Active
                && !p.IsSystem
                && (adminProfileId == null || p.UserId != adminProfileId))
            .ToListAsync(ct);

        var adminProfile = adminProfileId.HasValue
            ? await db.Profiles.FirstOrDefaultAsync(p => p.UserId == adminProfileId, ct)
            : null;

        foreach (var profile in profiles)
        {
            db.XpEntries.Add(new XpEntry
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                Amount = amount,
                Reason = reason,
                CreatedAt = DateTime.UtcNow,
            });
            profile.XpTotal += amount;
            profile.LastActiveAt = DateTimeOffset.UtcNow;

            // Registrar reconocimiento por el otorgamiento de XP.
            db.Recognitions.Add(new Recognition
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                TypeLabel = reason ?? "Puntos XP",
                Xp = amount,
                Status = RecognitionStatus.Sent,
                CreatedAt = DateTimeOffset.UtcNow,
                TriggeredByProfileId = adminProfile?.Id,
            });
        }
        await db.SaveChangesAsync(ct);

        // Evento de sistema (sin perfil) para el feed en vivo.
        var feedEvent = new FeedEvent
        {
            Id = Guid.NewGuid(),
            ProfileId = null,
            Kind = FeedEventKind.Racha,
            Body = reason,
            CreatedAt = DateTime.UtcNow,
        };
        db.FeedEvents.Add(feedEvent);
        await db.SaveChangesAsync(ct);
        await sender.SendAsync("feed_event_added", feedEvent);

        return profiles;
    }

    // --- Seguimiento ---

    public async Task<Profile> FollowUser(
        Guid profileId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        if (profileId == profile.Id) throw new GraphQLException("No puedes seguirte a ti mismo.");
        var target = await db.Profiles.FirstOrDefaultAsync(p => p.Id == profileId, ct)
            ?? throw new GraphQLException("No se encontró el perfil.");
        if (target.Status != ProfileStatus.Active)
            throw new GraphQLException("Este perfil no está disponible.");
        if (!await db.Follows.AnyAsync(f => f.FollowerProfileId == profile.Id && f.FollowingProfileId == profileId, ct))
        {
            db.Follows.Add(new Follow
            {
                Id = Guid.NewGuid(),
                FollowerProfileId = profile.Id,
                FollowingProfileId = profileId,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }
        return target;
    }

    public async Task<Profile> UnfollowUser(
        Guid profileId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var target = await db.Profiles.FirstOrDefaultAsync(p => p.Id == profileId, ct)
            ?? throw new GraphQLException("No se encontró el perfil.");
        var follow = await db.Follows.FirstOrDefaultAsync(
            f => f.FollowerProfileId == profile.Id && f.FollowingProfileId == profileId, ct);
        if (follow is not null)
        {
            db.Follows.Remove(follow);
            await db.SaveChangesAsync(ct);
        }
        return target;
    }

    // --- Mensajería privada ---

    /// <summary>
    /// Valida que <paramref name="otherProfileId"/> sea amigo mutuo del perfil
    /// <paramref name="myProfileId"/>: yo lo sigo Y él me sigue (mismo criterio que SendMessage).
    /// </summary>
    private static async Task AssertMutualFriendAsync(
        CommunityDbContext db, Guid myProfileId, Guid otherProfileId, CancellationToken ct)
    {
        var iFollow = await db.Follows.AnyAsync(
            f => f.FollowerProfileId == myProfileId && f.FollowingProfileId == otherProfileId, ct);
        var followsMe = await db.Follows.AnyAsync(
            f => f.FollowerProfileId == otherProfileId && f.FollowingProfileId == myProfileId, ct);
        if (!iFollow || !followsMe)
            throw new GraphQLException("Solo puedes añadir a tus amigos de la comunidad.");
    }

    // --- Chats grupales ---

    public async Task<ChatGroup> CreateGroup(
        string name,
        List<Guid> memberProfileIds,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        name = name.Trim();
        if (name.Length < 3 || name.Length > 100)
            throw new GraphQLException("El nombre del grupo debe tener entre 3 y 100 caracteres.");

        var requested = (memberProfileIds ?? []).Distinct().ToList();
        if (requested.Count == 0)
            throw new GraphQLException("Agrega al menos un miembro.");

        // El creador siempre forma parte del grupo (aunque no se pase en la lista).
        var memberSet = requested.ToList();
        if (!memberSet.Contains(profile.Id)) memberSet.Add(profile.Id);

        // Validamos a los demás miembros: deben existir, estar activos y ser amigos mutuos.
        var otherIds = requested.Where(id => id != profile.Id).ToList();
        if (otherIds.Count > 0)
        {
            var profiles = await db.Profiles.Where(p => otherIds.Contains(p.Id)).ToListAsync(ct);
            foreach (var id in otherIds)
            {
                var target = profiles.FirstOrDefault(p => p.Id == id);
                if (target is null || target.Status != ProfileStatus.Active)
                    throw new GraphQLException("Este perfil no está disponible.");
                await AssertMutualFriendAsync(db, profile.Id, id, ct);
            }
        }

        var group = new ChatGroup
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedByProfileId = profile.Id,
            CreatedAt = DateTime.UtcNow,
        };
        db.ChatGroups.Add(group);
        foreach (var id in memberSet)
        {
            db.ChatGroupMembers.Add(new ChatGroupMember
            {
                GroupId = group.Id,
                ProfileId = id,
                JoinedAt = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync(ct);
        await sender.SendAsync($"group_{group.Id}_changed", group);
        return group;
    }

    public async Task<ChatGroup> RenameGroup(
        Guid groupId,
        string name,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        name = name.Trim();
        if (name.Length < 3 || name.Length > 100)
            throw new GraphQLException("El nombre del grupo debe tener entre 3 y 100 caracteres.");

        var group = await db.ChatGroups.FirstOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new GraphQLException("No se encontró el grupo.");
        var isMember = await db.ChatGroupMembers.AnyAsync(
            m => m.GroupId == groupId && m.ProfileId == profile.Id, ct);
        if (!isMember) throw new GraphQLException("No eres miembro de este grupo.");

        group.Name = name;
        await db.SaveChangesAsync(ct);
        await sender.SendAsync($"group_{group.Id}_changed", group);
        return group;
    }

    public async Task<ChatGroup> AddGroupMember(
        Guid groupId,
        Guid profileId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var group = await db.ChatGroups.FirstOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new GraphQLException("No se encontró el grupo.");
        var isMember = await db.ChatGroupMembers.AnyAsync(
            m => m.GroupId == groupId && m.ProfileId == profile.Id, ct);
        if (!isMember) throw new GraphQLException("No eres miembro de este grupo.");

        var target = await db.Profiles.FirstOrDefaultAsync(p => p.Id == profileId, ct)
            ?? throw new GraphQLException("Este perfil no está disponible.");
        if (target.Status != ProfileStatus.Active)
            throw new GraphQLException("Este perfil no está disponible.");

        var already = await db.ChatGroupMembers.AnyAsync(
            m => m.GroupId == groupId && m.ProfileId == profileId, ct);
        if (already) throw new GraphQLException("Ya es miembro de este grupo.");

        await AssertMutualFriendAsync(db, profile.Id, profileId, ct);

        db.ChatGroupMembers.Add(new ChatGroupMember
        {
            GroupId = groupId,
            ProfileId = profileId,
            JoinedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        await sender.SendAsync($"group_{group.Id}_changed", group);
        return group;
    }

    public async Task<ChatGroup> RemoveGroupMember(
        Guid groupId,
        Guid profileId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var group = await db.ChatGroups.FirstOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new GraphQLException("No se encontró el grupo.");
        var isMember = await db.ChatGroupMembers.AnyAsync(
            m => m.GroupId == groupId && m.ProfileId == profile.Id, ct);
        if (!isMember) throw new GraphQLException("No eres miembro de este grupo.");
        if (profileId == group.CreatedByProfileId)
            throw new GraphQLException("No puedes quitar al creador del grupo.");
        if (profileId == profile.Id)
            throw new GraphQLException("Usa Salir del grupo para abandonarlo.");

        var target = await db.ChatGroupMembers.FirstOrDefaultAsync(
            m => m.GroupId == groupId && m.ProfileId == profileId, ct)
            ?? throw new GraphQLException("Este perfil no es miembro del grupo.");
        db.ChatGroupMembers.Remove(target);
        await db.SaveChangesAsync(ct);
        await sender.SendAsync($"group_{group.Id}_changed", group);
        return group;
    }

    public async Task<ChatGroup> LeaveGroup(
        Guid groupId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        var group = await db.ChatGroups.FirstOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new GraphQLException("No se encontró el grupo.");
        var membership = await db.ChatGroupMembers.FirstOrDefaultAsync(
            m => m.GroupId == groupId && m.ProfileId == profile.Id, ct)
            ?? throw new GraphQLException("No eres miembro de este grupo.");

        db.ChatGroupMembers.Remove(membership);
        await db.SaveChangesAsync(ct);

        var remaining = await db.ChatGroupMembers.CountAsync(m => m.GroupId == groupId, ct);
        if (remaining == 0)
        {
            // El grupo se queda vacío: se elimina (las membresías y mensajes se borran en cascada).
            db.ChatGroups.Remove(group);
            await db.SaveChangesAsync(ct);
            return group;
        }

        await sender.SendAsync($"group_{group.Id}_changed", group);
        return group;
    }

    public async Task<Message> SendGroupMessage(
        Guid groupId,
        string body,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        body = body.Trim();
        if (body.Length == 0) throw new GraphQLException("Escribe un mensaje.");
        if (body.Length > 1000) throw new GraphQLException("El mensaje no puede superar los 1000 caracteres.");

        var isMember = await db.ChatGroupMembers.AnyAsync(
            m => m.GroupId == groupId && m.ProfileId == profile.Id, ct);
        if (!isMember) throw new GraphQLException("No eres miembro de este grupo.");

        var message = new Message
        {
            Id = Guid.NewGuid(),
            SenderProfileId = profile.Id,
            ConversationId = groupId,
            RecipientProfileId = null,
            Body = body,
            CreatedAt = DateTime.UtcNow,
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);
        await sender.SendAsync($"group_{groupId}", message);
        return message;
    }

    public async Task<Message> SendMessage(
        Guid recipientProfileId,
        string body,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireProfileAsync(db, http, ct);
        body = body.Trim();
        if (body.Length == 0) throw new GraphQLException("Escribe un mensaje.");
        if (body.Length > 1000) throw new GraphQLException("El mensaje no puede superar los 1000 caracteres.");
        var recipient = await db.Profiles.FirstOrDefaultAsync(p => p.Id == recipientProfileId, ct)
            ?? throw new GraphQLException("No se encontró el destinatario.");
        if (recipient.Status != ProfileStatus.Active)
            throw new GraphQLException("Este perfil no está disponible.");
        var iFollow = await db.Follows.AnyAsync(
            f => f.FollowerProfileId == profile.Id && f.FollowingProfileId == recipientProfileId, ct);
        var followsMe = await db.Follows.AnyAsync(
            f => f.FollowerProfileId == recipientProfileId && f.FollowingProfileId == profile.Id, ct);
        if (!iFollow || !followsMe)
            throw new GraphQLException("Solo puedes escribir a tus amigos de la comunidad.");

        var message = new Message
        {
            Id = Guid.NewGuid(),
            SenderProfileId = profile.Id,
            RecipientProfileId = recipientProfileId,
            Body = body,
            CreatedAt = DateTime.UtcNow,
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);

        var key = string.Join(':', new[] { profile.Id.ToString(), recipientProfileId.ToString() }
            .OrderBy(x => x, StringComparer.Ordinal));
        await sender.SendAsync($"message_{key}", message);
        return message;
    }

    // --- Mensajería desde sistema (admin) ---

    /// <summary>
    /// Envía un mensaje masivo desde el perfil del sistema a miembros del alcance
    /// especificado. Requiere el permiso Community.Manage.
    /// </summary>
    [Authorize(Policy = "Community.Manage")]
    public async Task<int> SendBulkMessage(
        MessageScope scope,
        string body,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        body = body.Trim();
        if (body.Length == 0) throw new GraphQLException("Escribe un mensaje.");
        if (body.Length > 1000) throw new GraphQLException("El mensaje no puede superar los 1000 caracteres.");

        var systemProfile = await GetSystemProfileAsync(db, ct);
        var adminProfile = await RequireProfileAsync(db, http, ct);
        var now = DateTime.UtcNow;
        var threshold7d = DateTimeOffset.UtcNow.AddDays(-7);

        // Resolver destinatarios según el alcance.
        List<Profile> targets;
        switch (scope)
        {
            case MessageScope.Inactive:
                // Mismo criterio que DashboardAggregator: max(LastPostAt, LastActiveAt) es null o < now-7d.
                targets = await db.Profiles
                    .Where(p => p.Status == ProfileStatus.Active && !p.IsSystem)
                    .ToListAsync(ct);
                targets = targets.Where(p =>
                {
                    var last = p.LastPostAt.HasValue && p.LastActiveAt.HasValue
                        ? (p.LastPostAt.Value > p.LastActiveAt.Value ? p.LastPostAt.Value : p.LastActiveAt.Value)
                        : p.LastPostAt ?? p.LastActiveAt;
                    return last is null || last.Value.UtcDateTime < threshold7d.UtcDateTime;
                }).ToList();
                break;

            case MessageScope.AllActive:
                targets = await db.Profiles
                    .Where(p => p.Status == ProfileStatus.Active && !p.IsSystem)
                    .ToListAsync(ct);
                break;

            default:
                throw new GraphQLException("Alcance no válido.");
        }

        // Nunca enviar al propio admin (su perfil autoprovisionado suele quedar "inactivo").
        targets = targets.Where(p => p.Id != adminProfile.Id).ToList();

        if (targets.Count == 0) return 0;

        // Insertar un mensaje por destinatario.
        foreach (var target in targets)
        {
            db.Messages.Add(new Message
            {
                Id = Guid.NewGuid(),
                SenderProfileId = systemProfile.Id,
                RecipientProfileId = target.Id,
                Body = body,
                CreatedAt = now,
                TriggeredByProfileId = adminProfile.Id,
            });
        }
        await db.SaveChangesAsync(ct);

        // Emitir un evento de feed resumen (uno solo).
        var feedEvent = new FeedEvent
        {
            Id = Guid.NewGuid(),
            ProfileId = systemProfile.Id,
            Kind = FeedEventKind.Mensaje,
            Body = $"Mensaje enviado a {targets.Count} miembros",
            CreatedAt = now,
        };
        db.FeedEvents.Add(feedEvent);
        await db.SaveChangesAsync(ct);
        await sender.SendAsync("feed_event_added", feedEvent);

        return targets.Count;
    }

    /// <summary>
    /// Envía un mensaje directo desde el perfil del sistema a un miembro concreto.
    /// No requiere relación de amistad. Requiere el permiso Community.Manage.
    /// </summary>
    [Authorize(Policy = "Community.Manage")]
    public async Task<Message> SendDirectMessage(
        Guid profileId,
        string body,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        body = body.Trim();
        if (body.Length == 0) throw new GraphQLException("Escribe un mensaje.");
        if (body.Length > 1000) throw new GraphQLException("El mensaje no puede superar los 1000 caracteres.");

        var systemProfile = await GetSystemProfileAsync(db, ct);
        var adminProfile = await RequireProfileAsync(db, http, ct);

        var recipient = await db.Profiles.FirstOrDefaultAsync(p => p.Id == profileId, ct)
            ?? throw new GraphQLException("No se encontró el destinatario.");
        if (recipient.Status != ProfileStatus.Active)
            throw new GraphQLException("Este perfil no está disponible.");

        var now = DateTime.UtcNow;
        var message = new Message
        {
            Id = Guid.NewGuid(),
            SenderProfileId = systemProfile.Id,
            RecipientProfileId = profileId,
            Body = body,
            CreatedAt = now,
            TriggeredByProfileId = adminProfile.Id,
        };
        db.Messages.Add(message);

        // Emitir evento de feed.
        var feedEvent = new FeedEvent
        {
            Id = Guid.NewGuid(),
            ProfileId = systemProfile.Id,
            Kind = FeedEventKind.Mensaje,
            Body = body.Length > 500 ? body[..500] : body,
            CreatedAt = now,
        };
        db.FeedEvents.Add(feedEvent);
        await db.SaveChangesAsync(ct);

        await sender.SendAsync("feed_event_added", feedEvent);
        return message;
    }

    /// <summary>
    /// Resuelve el perfil del sistema (IsSystem == true). Lanza error si no existe.
    /// </summary>
    private static async Task<Profile> GetSystemProfileAsync(CommunityDbContext db, CancellationToken ct)
        => await db.Profiles.FirstOrDefaultAsync(p => p.IsSystem, ct)
           ?? throw new GraphQLException("El perfil del sistema no está configurado.");

    private static async Task<Profile> RequireProfileAsync(
        CommunityDbContext db, IHttpContextAccessor http, CancellationToken ct)
    {
        var userId = CommunityQuery.CurrentUserId(http);
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new GraphQLException("Crea tu perfil de comunidad primero.");
        if (profile.Status != ProfileStatus.Active)
            throw new GraphQLException("Tu perfil está suspendido en la comunidad.");
        return profile;
    }

    /// <summary>
    /// Recalcula la racha actual y la mejor racha del perfil a partir de sus
    /// publicaciones (días consecutivos con publicación). Actualiza las columnas
    /// almacenadas para permitir ordenamiento en SQL.
    /// </summary>
    private static async Task RecomputeStreakAsync(
        CommunityDbContext db, Profile profile, CancellationToken ct)
    {
        var dates = await db.Posts
            .Where(p => p.ProfileId == profile.Id && p.DeletedAt == null)
            .Select(p => p.CreatedAt)
            .ToListAsync(ct);

        profile.CurrentStreak = CommunityStats.CurrentStreak(dates);
        profile.BestStreak = Math.Max(profile.BestStreak, CommunityStats.BestStreak(dates));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Mapea un PostType al FeedEventKind correspondiente.</summary>
    private static FeedEventKind MapPostTypeToFeedEvent(PostType type) => type switch
    {
        PostType.Imagen => FeedEventKind.Foto,
        PostType.Video => FeedEventKind.Video,
        PostType.Encuesta => FeedEventKind.Publicacion,
        PostType.Logro => FeedEventKind.Logro,
        _ => FeedEventKind.Publicacion,
    };
}

