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
using System.Security.Claims;

namespace CoppAddresd.Community.GraphQL.Mutations;

/// <summary>
/// Mutaciones del módulo de clubes (contrato D1 congelado + ampliación de la app:
/// like/comentario/voto en posts de club, unirse/salir por visibilidad e invitación).
/// Reglas: crear/gestionar club requiere <c>Community.Manage</c>; publicar/moderar
/// requiere rol Admin/Moderador del club; comentar/votar/asistir requiere membresía
/// Activa (no silenciado/expulsado). Los contadores de cupos son atómicos
/// (anti-overbooking por UPDATE condicional).
/// </summary>
[ExtendObjectType(typeof(CommunityMutation))]
[Authorize]
public sealed class ClubMutation
{
    // ─── CLUBES (CRUD) ──────────────────────────────────────────────────

    /// <summary>Crea un club; el creador queda como Admin. Requiere Community.Manage.</summary>
    [Authorize(Policy = "Community.Manage")]
    public async Task<Club> CreateClub(
        ClubInput input,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);

        if (string.IsNullOrWhiteSpace(input.Name))
            throw new GraphQLException("El nombre del club es obligatorio.");
        if (string.IsNullOrWhiteSpace(input.Category))
            throw new GraphQLException("La categoría del club es obligatoria.");

        var slug = (input.Slug ?? Slugify(input.Name));
        if (await db.Clubs.AnyAsync(c => c.Slug == slug, ct))
            throw new GraphQLException("Ya existe un club con ese slug.");

        var club = new Club
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = input.Name.Trim(),
            Description = input.Description ?? "",
            Rules = input.Rules ?? [],
            Objectives = input.Objectives ?? [],
            Category = input.Category.Trim(),
            Tags = input.Tags ?? [],
            CoverKey = input.CoverKey,
            LogoKey = input.LogoKey,
            Visibility = input.Visibility,
            MaxMembers = input.MaxMembers,
            CreatedByProfileId = profile.Id,
            CreatedAt = DateTime.UtcNow,
        };
        db.Clubs.Add(club);

        db.ClubMembers.Add(new ClubMember
        {
            ClubId = club.Id,
            ProfileId = profile.Id,
            Role = ClubMemberRole.Admin,
            Status = ClubMemberStatus.Activo,
            JoinedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);
        await sender.SendAsync($"club_member_{profile.Id}", club, ct);
        return club;
    }

    /// <summary>Actualiza los datos de un club. Requiere Community.Manage.</summary>
    [Authorize(Policy = "Community.Manage")]
    public async Task<Club> UpdateClub(
        Guid id,
        ClubInput input,
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new GraphQLException("No se encontró el club.");

        if (!string.IsNullOrWhiteSpace(input.CoverKey) && !PostStorageEndpoints.IsCoverKey(input.CoverKey))
            throw new GraphQLException("La portada no es válida.");
        if (!string.IsNullOrWhiteSpace(input.LogoKey) && !PostStorageEndpoints.IsLogoKey(input.LogoKey))
            throw new GraphQLException("El logo no es válido.");

        club.Name = input.Name?.Trim() ?? club.Name;
        club.Description = input.Description ?? club.Description;
        club.Rules = input.Rules ?? club.Rules;
        club.Objectives = input.Objectives ?? club.Objectives;
        club.Category = input.Category?.Trim() ?? club.Category;
        club.Tags = input.Tags ?? club.Tags;
        club.CoverKey = input.CoverKey ?? club.CoverKey;
        club.LogoKey = input.LogoKey ?? club.LogoKey;
        club.Visibility = input.Visibility;
        club.MaxMembers = input.MaxMembers ?? club.MaxMembers;
        club.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return club;
    }

    /// <summary>Archiva un club (deja de aparecer en exploración). Requiere Community.Manage.</summary>
    [Authorize(Policy = "Community.Manage")]
    public async Task<Club> ArchiveClub(
        Guid id,
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new GraphQLException("No se encontró el club.");
        if (club.IsSystem)
            throw new GraphQLException("Los clubes de sistema no pueden archivarse.");

        club.Status = ClubStatus.Archivado;
        club.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return club;
    }

    /// <summary>Restaura un club archivado. Requiere Community.Manage.</summary>
    [Authorize(Policy = "Community.Manage")]
    public async Task<Club> UnarchiveClub(
        Guid id,
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new GraphQLException("No se encontró el club.");

        club.Status = ClubStatus.Activo;
        club.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return club;
    }

    /// <summary>
    /// Crea la información de subida de la portada o el logo del club:
    /// clave, URL de subida y URL de lectura firmada (patrón de avatar/portada).
    /// Con clubId exige rol Admin/Moderador del club; sin clubId (creación de
    /// club) exige Community.Manage.
    /// </summary>
    public async Task<PostImageUploadInfo> CreateClubMediaUploadInfo(
        Guid? clubId,
        string kind,
        string fileName,
        string contentType,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] IObjectStorageService storage,
        [Service] IConfiguration config,
        [Service] StorageSignatureService signer,
        CancellationToken ct)
    {
        if (clubId is not null)
        {
            var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId && c.Status == ClubStatus.Activo, ct)
                ?? throw new GraphQLException("No se encontró el club.");

            var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == CommunityQuery.CurrentUserId(http), ct)
                ?? throw new GraphQLException("Crea tu perfil de comunidad primero.");
            if (!HasPermission(http, "Community.Manage"))
            {
                await RequireClubRoleAsync(db, club.Id, profile.Id, ClubMemberRole.Moderador, ct, http);
            }
        }
        else if (!HasPermission(http, "Community.Manage"))
        {
            throw new GraphQLException("No tienes permisos para subir imágenes de club.");
        }

        var isLogo = kind?.Equals("LOGO", StringComparison.OrdinalIgnoreCase) == true;
        var prefix = isLogo ? PostStorageEndpoints.LogoPrefix : PostStorageEndpoints.CoverPrefix;

        var normalizedContentType = contentType.Split(';')[0].Trim().ToLowerInvariant();
        if (!PostStorageEndpoints.IsAllowedImageContentType(normalizedContentType)
            || normalizedContentType.StartsWith("video/"))
            throw new GraphQLException("La portada/logo debe ser una imagen (JPG, PNG, WEBP, GIF, HEIC).");

        var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        if (!PostStorageEndpoints.IsAllowedImageExtension(extension)
            || extension is ".mp4" or ".webm")
            throw new GraphQLException("Formato no permitido para la imagen.");

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

    // ─── MEMBRESÍA ──────────────────────────────────────────────────────

    /// <summary>Unirse directamente a un club público (con cupo disponible).</summary>
    public async Task<ClubMember> JoinClubDirect(
        Guid clubId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId && c.Status == ClubStatus.Activo, ct)
            ?? throw new GraphQLException("No se encontró el club.");
        if (club.Visibility != ClubVisibility.Publico)
            throw new GraphQLException("Este club no permite unirse directamente.");

        var activeCount = await db.ClubMembers.CountAsync(m => m.ClubId == clubId && m.Status == ClubMemberStatus.Activo, ct);
        if (club.MaxMembers is not null && activeCount >= club.MaxMembers)
            throw new GraphQLException("El club alcanzó su capacidad máxima.");

        return await CreateOrReactivateMembershipAsync(db, club, profile, ClubMemberStatus.Activo, ClubMemberRole.Miembro, sender, ct);
    }

    /// <summary>Solicitar ingreso a un club privado (queda Pendiente para aprobación).</summary>
    public async Task<ClubMember> RequestMembership(
        Guid clubId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId && c.Status == ClubStatus.Activo, ct)
            ?? throw new GraphQLException("No se encontró el club.");
        if (club.Visibility != ClubVisibility.Privado)
            throw new GraphQLException("Este club no requiere solicitud de ingreso.");

        var existing = await db.ClubMembers.FirstOrDefaultAsync(m => m.ClubId == clubId && m.ProfileId == profile.Id, ct);
        if (existing is not null)
        {
            if (existing.Status == ClubMemberStatus.Pendiente) return existing;
            if (existing.Status == ClubMemberStatus.Expulsado)
                throw new GraphQLException("Fuiste expulsado de este club.");
            return existing;
        }

        var member = new ClubMember
        {
            ClubId = clubId,
            ProfileId = profile.Id,
            Role = ClubMemberRole.Miembro,
            Status = ClubMemberStatus.Pendiente,
            JoinedAt = DateTime.UtcNow,
        };
        db.ClubMembers.Add(member);
        await db.SaveChangesAsync(ct);
        await sender.SendAsync($"club_member_{profile.Id}", member, ct);
        return member;
    }

    /// <summary>Unirse con una invitación por token (válida, sin usar, no expirada).</summary>
    public async Task<ClubMember> JoinWithInvitation(
        string token,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var invitation = await db.ClubInvitations
            .FirstOrDefaultAsync(i => i.Token == token && i.UsedAt == null && i.ExpiresAt > DateTime.UtcNow, ct)
            ?? throw new GraphQLException("La invitación no es válida o ya expiró.");
        if (invitation.ProfileId is not null && invitation.ProfileId != profile.Id)
            throw new GraphQLException("Esta invitación es para otro perfil.");

        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == invitation.ClubId && c.Status == ClubStatus.Activo, ct)
            ?? throw new GraphQLException("El club de la invitación ya no está activo.");

        var member = await CreateOrReactivateMembershipAsync(db, club, profile, ClubMemberStatus.Activo, ClubMemberRole.Miembro, sender, ct);

        invitation.UsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return member;
    }

    /// <summary>Salir del club (borra la membresía).</summary>
    public async Task<bool> LeaveClub(
        Guid clubId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var membership = await db.ClubMembers
            .FirstOrDefaultAsync(m => m.ClubId == clubId && m.ProfileId == profile.Id, ct)
            ?? throw new GraphQLException("No eres miembro de este club.");
        if (membership.Role == ClubMemberRole.Admin)
        {
            var admins = await db.ClubMembers.CountAsync(m => m.ClubId == clubId && m.Role == ClubMemberRole.Admin && m.Status == ClubMemberStatus.Activo, ct);
            if (admins <= 1)
                throw new GraphQLException("Eres el único administrador del club; transfiere la gestión antes de salir.");
        }

        db.ClubMembers.Remove(membership);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Agrega un perfil de la plataforma como miembro del club (rol opcional).
    /// Requiere Admin/Moderador del club o Community.Manage.</summary>
    public async Task<ClubMember> AddClubMember(
        Guid clubId,
        Guid profileId,
        ClubMemberRole role,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var actor = await RequireMyProfileAsync(db, http, ct);
        await RequireClubRoleAsync(db, clubId, actor.Id, ClubMemberRole.Moderador, ct, http);

        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId && c.Status == ClubStatus.Activo, ct)
            ?? throw new GraphQLException("No se encontró el club.");
        var target = await db.Profiles.FirstOrDefaultAsync(p => p.Id == profileId, ct)
            ?? throw new GraphQLException("El perfil no existe en la plataforma.");

        if (role == ClubMemberRole.Admin && actor.Id == profileId)
            throw new GraphQLException("No puedes asignarte a ti mismo un rol de administrador.");

        var existing = await db.ClubMembers.FirstOrDefaultAsync(m => m.ClubId == clubId && m.ProfileId == profileId, ct);
        if (existing is not null)
        {
            if (existing.Status == ClubMemberStatus.Expulsado)
                throw new GraphQLException("El perfil fue expulsado de este club.");
            existing.Status = ClubMemberStatus.Activo;
            existing.Role = role;
            existing.MutedUntil = null;
            existing.JoinedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await sender.SendAsync($"club_member_{profileId}", existing, ct);
            return existing;
        }

        var activeCount = await db.ClubMembers.CountAsync(m => m.ClubId == clubId && m.Status == ClubMemberStatus.Activo, ct);
        if (club.MaxMembers is not null && activeCount >= club.MaxMembers)
            throw new GraphQLException("El club alcanzó su capacidad máxima.");

        var member = new ClubMember
        {
            ClubId = clubId,
            ProfileId = profileId,
            Role = role,
            Status = ClubMemberStatus.Activo,
            JoinedAt = DateTime.UtcNow,
        };
        db.ClubMembers.Add(member);
        db.ModerationLogs.Add(new ModerationLog
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            ActorProfileId = actor.Id,
            TargetProfileId = profileId,
            Action = "MiembroAgregado",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        await sender.SendAsync($"club_member_{profileId}", member, ct);
        return member;
    }

    /// <summary>Aprueba una solicitud pendiente (Pendiente → Activo) y registra FeedEvent + notificación.</summary>
    public async Task<ClubMember> ApproveMembership(
        Guid clubId,
        Guid profileId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var actor = await RequireMyProfileAsync(db, http, ct);
        await RequireClubRoleAsync(db, clubId, actor.Id, ClubMemberRole.Admin, ct, http);

        var member = await db.ClubMembers
            .FirstOrDefaultAsync(m => m.ClubId == clubId && m.ProfileId == profileId && m.Status == ClubMemberStatus.Pendiente, ct)
            ?? throw new GraphQLException("No hay solicitud pendiente de ese perfil.");

        member.Status = ClubMemberStatus.Activo;
        await db.SaveChangesAsync(ct);

        db.ModerationLogs.Add(new ModerationLog
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            ActorProfileId = actor.Id,
            TargetProfileId = profileId,
            Action = "SolicitudAprobada",
            CreatedAt = DateTime.UtcNow,
        });
        db.ClubNotifications.Add(new ClubNotification
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            ProfileId = profileId,
            Type = ClubNotificationType.SolicitudAprobada,
            Payload = $"{{\"clubId\":\"{clubId}\"}}",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        await sender.SendAsync($"club_member_{profileId}", member, ct);
        return member;
    }

    /// <summary>Rechaza una solicitud pendiente (borra la membresía) y registra el log.</summary>
    public async Task<bool> RejectMembership(
        Guid clubId,
        Guid profileId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var actor = await RequireMyProfileAsync(db, http, ct);
        await RequireClubRoleAsync(db, clubId, actor.Id, ClubMemberRole.Admin, ct, http);

        var member = await db.ClubMembers
            .FirstOrDefaultAsync(m => m.ClubId == clubId && m.ProfileId == profileId && m.Status == ClubMemberStatus.Pendiente, ct)
            ?? throw new GraphQLException("No hay solicitud pendiente de ese perfil.");

        db.ClubMembers.Remove(member);
        db.ModerationLogs.Add(new ModerationLog
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            ActorProfileId = actor.Id,
            TargetProfileId = profileId,
            Action = "SolicitudRechazada",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Expulsa a un miembro (borra la membresía) y registra el log.</summary>
    public async Task<bool> ExpelMember(
        Guid clubId,
        Guid profileId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var actor = await RequireMyProfileAsync(db, http, ct);
        await RequireClubRoleAsync(db, clubId, actor.Id, ClubMemberRole.Moderador, ct, http);

        var member = await db.ClubMembers.FirstOrDefaultAsync(m => m.ClubId == clubId && m.ProfileId == profileId, ct)
            ?? throw new GraphQLException("Ese perfil no es miembro del club.");
        if (member.Role == ClubMemberRole.Admin && member.ProfileId != actor.Id)
            throw new GraphQLException("No puedes expulsar a un administrador.");

        db.ClubMembers.Remove(member);
        db.ModerationLogs.Add(new ModerationLog
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            ActorProfileId = actor.Id,
            TargetProfileId = profileId,
            Action = "Expulsion",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Silencia a un miembro hasta una fecha (no puede comentar/votar).</summary>
    public async Task<ClubMember> MuteMember(
        Guid clubId,
        Guid profileId,
        DateTime until,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var actor = await RequireMyProfileAsync(db, http, ct);
        await RequireClubRoleAsync(db, clubId, actor.Id, ClubMemberRole.Moderador, ct, http);

        var member = await db.ClubMembers.FirstOrDefaultAsync(m => m.ClubId == clubId && m.ProfileId == profileId, ct)
            ?? throw new GraphQLException("Ese perfil no es miembro del club.");
        if (until <= DateTime.UtcNow)
            throw new GraphQLException("La fecha de silencio debe ser futura.");

        member.Status = ClubMemberStatus.Silenciado;
        member.MutedUntil = until;
        db.ModerationLogs.Add(new ModerationLog
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            ActorProfileId = actor.Id,
            TargetProfileId = profileId,
            Action = "Silenciado",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return member;
    }

    /// <summary>Quita el silencio a un miembro.</summary>
    public async Task<ClubMember> UnmuteMember(
        Guid clubId,
        Guid profileId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var actor = await RequireMyProfileAsync(db, http, ct);
        await RequireClubRoleAsync(db, clubId, actor.Id, ClubMemberRole.Moderador, ct, http);

        var member = await db.ClubMembers.FirstOrDefaultAsync(m => m.ClubId == clubId && m.ProfileId == profileId, ct)
            ?? throw new GraphQLException("Ese perfil no es miembro del club.");

        member.Status = ClubMemberStatus.Activo;
        member.MutedUntil = null;
        db.ModerationLogs.Add(new ModerationLog
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            ActorProfileId = actor.Id,
            TargetProfileId = profileId,
            Action = "SilencioRetirado",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return member;
    }

    /// <summary>Cambia el rol de un miembro (solo Admin).</summary>
    public async Task<ClubMember> ChangeMemberRole(
        Guid clubId,
        Guid profileId,
        ClubMemberRole role,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var actor = await RequireMyProfileAsync(db, http, ct);
        await RequireClubRoleAsync(db, clubId, actor.Id, ClubMemberRole.Admin, ct, http);
        if (role == ClubMemberRole.Admin && profileId == actor.Id)
            throw new GraphQLException("No puedes cambiar tu propio rol de administrador.");

        var member = await db.ClubMembers.FirstOrDefaultAsync(m => m.ClubId == clubId && m.ProfileId == profileId, ct)
            ?? throw new GraphQLException("Ese perfil no es miembro del club.");

        member.Role = role;
        await db.SaveChangesAsync(ct);
        return member;
    }

    /// <summary>Crea una invitación por token (para enlace/QR). Requiere Admin del club.</summary>
    public async Task<ClubInvitation> CreateInvitation(
        Guid clubId,
        Guid? profileId,
        DateTime? expiresAt,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var actor = await RequireMyProfileAsync(db, http, ct);
        await RequireClubRoleAsync(db, clubId, actor.Id, ClubMemberRole.Admin, ct, http);

        var invitation = new ClubInvitation
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            ProfileId = profileId,
            Token = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant(),
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(7),
            CreatedByProfileId = actor.Id,
            CreatedAt = DateTime.UtcNow,
        };
        db.ClubInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);
        return invitation;
    }

    // ─── PUBLICACIONES DEL CLUB ─────────────────────────────────────────

    /// <summary>Publica en el club (solo Admin/Moderador). Soporta borrador, programación y fijado.</summary>
    public async Task<Post> CreateClubPost(
        Guid clubId,
        ClubPostInput input,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        await RequireClubRoleAsync(db, clubId, profile.Id, ClubMemberRole.Moderador, ct, http);

        if (string.IsNullOrWhiteSpace(input.Body))
            throw new GraphQLException("El texto de la publicación es obligatorio.");
        if (!string.IsNullOrWhiteSpace(input.ImageKey) && !PostStorageEndpoints.IsValidPostImageKey(input.ImageKey))
            throw new GraphQLException("La imagen adjunta no es válida.");

        var effectiveType = input.Type ?? (!string.IsNullOrWhiteSpace(input.ImageKey)
            ? System.IO.Path.GetExtension(input.ImageKey).ToLowerInvariant() is ".mp4" or ".webm" ? PostType.Video : PostType.Imagen
            : PostType.Texto);

        var scheduled = input.ScheduledFor.HasValue && input.ScheduledFor.Value > DateTime.UtcNow;

        var post = new Post
        {
            Id = Guid.NewGuid(),
            ProfileId = profile.Id,
            ClubId = clubId,
            Body = input.Body.Trim(),
            ImageKey = string.IsNullOrWhiteSpace(input.ImageKey) ? null : input.ImageKey,
            Type = effectiveType,
            ClubVisibility = input.Visibility ?? ClubPostVisibility.Publico,
            ClubStatus = scheduled ? ClubPostStatus.Programado : ClubPostStatus.Publicado,
            ScheduledFor = scheduled ? input.ScheduledFor!.Value : null,
            Pinned = input.Pinned ?? false,
            Featured = input.Featured ?? false,
            CreatedAt = DateTime.UtcNow,
        };

        if (post.Pinned)
        {
            var maxOrder = await db.Posts.Where(p => p.ClubId == clubId && p.Pinned)
                .Select(p => (int?)p.PinnedOrder).MaxAsync(ct) ?? 0;
            post.PinnedOrder = maxOrder + 1;
        }

        db.Posts.Add(post);
        await db.SaveChangesAsync(ct);

        if (post.ClubStatus == ClubPostStatus.Publicado)
        {
            await sender.SendAsync($"club_{clubId}_posts", post, ct);
        }
        return post;
    }

    /// <summary>Actualiza una publicación del club (solo Admin/Moderador).</summary>
    public async Task<Post> UpdateClubPost(
        Guid id,
        ClubPostInput input,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == id && p.ClubId != null, ct)
            ?? throw new GraphQLException("No se encontró la publicación.");

        await RequireClubRoleAsync(db, post.ClubId!.Value, profile.Id, ClubMemberRole.Moderador, ct, http);

        post.Body = string.IsNullOrWhiteSpace(input.Body) ? post.Body : input.Body.Trim();
        post.Type = input.Type ?? post.Type;
        post.ClubVisibility = input.Visibility ?? post.ClubVisibility;
        if (input.ImageKey is not null)
        {
            if (!PostStorageEndpoints.IsValidPostImageKey(input.ImageKey))
                throw new GraphQLException("La imagen adjunta no es válida.");
            post.ImageKey = input.ImageKey;
        }
        post.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return post;
    }

    /// <summary>Publica una publicación programada vencida (o fuerza la publicación).</summary>
    public async Task<Post> PublishScheduledPost(
        Guid id,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == id && p.ClubId != null, ct)
            ?? throw new GraphQLException("No se encontró la publicación.");

        await RequireClubRoleAsync(db, post.ClubId!.Value, profile.Id, ClubMemberRole.Moderador, ct, http);

        post.ClubStatus = ClubPostStatus.Publicado;
        post.ScheduledFor = null;
        post.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await sender.SendAsync($"club_{post.ClubId}_posts", post, ct);
        return post;
    }

    /// <summary>Elimina (soft-delete) una publicación del club.</summary>
    public async Task<bool> DeleteClubPost(
        Guid id,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == id && p.ClubId != null, ct)
            ?? throw new GraphQLException("No se encontró la publicación.");

        await RequireClubRoleAsync(db, post.ClubId!.Value, profile.Id, ClubMemberRole.Moderador, ct, http);

        post.DeletedAt = DateTime.UtcNow;
        post.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Da/quita un like a una publicación del club (toggle). Requiere membresía Activa.</summary>
    public async Task<Post> ToggleClubPostLike(
        Guid postId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == postId && p.ClubId != null, ct)
            ?? throw new GraphQLException("No se encontró la publicación.");

        await RequireActiveMembershipAsync(db, post.ClubId!.Value, profile.Id, ct, http);

        var like = await db.Likes.FirstOrDefaultAsync(l => l.PostId == postId && l.ProfileId == profile.Id, ct);
        if (like is null)
        {
            db.Likes.Add(new Like { Id = Guid.NewGuid(), PostId = postId, ProfileId = profile.Id, CreatedAt = DateTime.UtcNow });
        }
        else
        {
            db.Likes.Remove(like);
        }

        await db.SaveChangesAsync(ct);
        await sender.SendAsync("post_added", post, ct);
        return post;
    }

    /// <summary>Comenta una publicación del club. Requiere membresía Activa y no estar silenciado.</summary>
    public async Task<Comment> AddClubComment(
        Guid postId,
        string body,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == postId && p.ClubId != null, ct)
            ?? throw new GraphQLException("No se encontró la publicación.");
        if (string.IsNullOrWhiteSpace(body))
            throw new GraphQLException("El comentario no puede estar vacío.");

        await RequireActiveMembershipAsync(db, post.ClubId!.Value, profile.Id, ct, http);

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            ProfileId = profile.Id,
            Body = body.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);
        await sender.SendAsync("comment_added", comment, ct);
        return comment;
    }

    /// <summary>Vota una encuesta del club (un voto por perfil; reemplaza el anterior).</summary>
    public async Task<Post> VoteClubPoll(
        Guid postId,
        Guid optionId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var post = await db.Posts
            .Include(p => p.Poll!).ThenInclude(poll => poll.Options)
            .FirstOrDefaultAsync(p => p.Id == postId && p.ClubId != null, ct)
            ?? throw new GraphQLException("No se encontró la publicación.");
        if (post.Poll is null)
            throw new GraphQLException("Esta publicación no tiene encuesta.");
        if (post.Poll.Options.All(o => o.Id != optionId))
            throw new GraphQLException("La opción no pertenece a esta encuesta.");

        await RequireActiveMembershipAsync(db, post.ClubId!.Value, profile.Id, ct, http);

        var optionIds = post.Poll.Options.Select(o => o.Id).ToList();
        var previous = await db.PollVotes.Where(v => optionIds.Contains(v.OptionId) && v.ProfileId == profile.Id).ToListAsync(ct);
        db.PollVotes.RemoveRange(previous);

        db.PollVotes.Add(new PollVote
        {
            Id = Guid.NewGuid(),
            OptionId = optionId,
            ProfileId = profile.Id,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return post;
    }

    // ─── EVENTOS ────────────────────────────────────────────────────────

    /// <summary>Crea un evento del club (solo Admin/Moderador).</summary>
    public async Task<ClubEvent> CreateClubEvent(
        Guid clubId,
        ClubEventInput input,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        await RequireClubRoleAsync(db, clubId, profile.Id, ClubMemberRole.Moderador, ct, http);
        if (string.IsNullOrWhiteSpace(input.Title))
            throw new GraphQLException("El título del evento es obligatorio.");
        if (input.EndsAt <= input.StartsAt)
            throw new GraphQLException("La fecha de fin debe ser posterior al inicio.");

        var @event = new ClubEvent
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            Title = input.Title.Trim(),
            Description = input.Description ?? "",
            Type = input.Type,
            StartsAt = input.StartsAt,
            EndsAt = input.EndsAt,
            Location = input.Location,
            MeetingUrl = input.MeetingUrl,
            MaxAttendees = input.MaxAttendees,
            CreatedAt = DateTime.UtcNow,
        };
        db.ClubEvents.Add(@event);
        await db.SaveChangesAsync(ct);

        // Notifica a los miembros del club.
        var memberIds = await db.ClubMembers
            .Where(m => m.ClubId == clubId && m.Status == ClubMemberStatus.Activo)
            .Select(m => m.ProfileId)
            .ToListAsync(ct);
        foreach (var memberId in memberIds)
        {
            db.ClubNotifications.Add(new ClubNotification
            {
                Id = Guid.NewGuid(),
                ClubId = clubId,
                ProfileId = memberId,
                Type = ClubNotificationType.NuevoEvento,
                Payload = $"{{\"eventId\":\"{@event.Id}\"}}",
                CreatedAt = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync(ct);
        return @event;
    }

    /// <summary>Confirma asistencia a un evento (anti-overbooking atómico: si no hay cupo, lista de espera).</summary>
    public async Task<EventAttendance> ConfirmAttendance(
        Guid eventId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var @event = await db.ClubEvents.FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw new GraphQLException("No se encontró el evento.");
        await RequireActiveMembershipAsync(db, @event.ClubId, profile.Id, ct, http);

        var existing = await db.EventAttendances.FirstOrDefaultAsync(a => a.EventId == eventId && a.ProfileId == profile.Id, ct);
        if (existing is not null)
        {
            if (existing.Status == EventAttendanceStatus.Confirmado || existing.Status == EventAttendanceStatus.CheckIn) return existing;
            if (existing.Status == EventAttendanceStatus.Cancelado)
            {
                // Reconfirma: incremento atómico.
                await ConfirmAtomicAsync(db, @event, ct);
                existing.Status = EventAttendanceStatus.Confirmado;
                existing.CreatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                await db.Entry(@event).ReloadAsync(ct);
                return existing;
            }
            return existing;
        }

        // UPDATE condicional: solo incrementa si hay cupo.
        var affected = await db.ClubEvents
            .Where(e => e.Id == eventId && (e.MaxAttendees == null || e.ConfirmedCount < e.MaxAttendees))
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.ConfirmedCount, e => e.ConfirmedCount + 1)
                .SetProperty(e => e.Status, e => ClubEventStatus.Abierto), ct);

        var attendance = new EventAttendance
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            ProfileId = profile.Id,
            Status = affected == 0 ? EventAttendanceStatus.ListaEspera : EventAttendanceStatus.Confirmado,
            CreatedAt = DateTime.UtcNow,
        };
        db.EventAttendances.Add(attendance);

        if (affected == 0)
        {
            await db.ClubEvents
                .Where(e => e.Id == eventId)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.WaitlistCount, e => e.WaitlistCount + 1), ct);
        }
        else if (await db.ClubEvents.Where(e => e.Id == eventId).Select(e => e.ConfirmedCount).FirstAsync(ct) >= @event.MaxAttendees)
        {
            await db.ClubEvents
                .Where(e => e.Id == eventId && e.MaxAttendees != null && e.ConfirmedCount >= e.MaxAttendees)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, e => ClubEventStatus.Lleno), ct);
        }

        await db.SaveChangesAsync(ct);
        // ExecuteUpdateAsync no actualiza el tracker: recargar para que lecturas
        // posteriores en el mismo DbContext vean los contadores reales.
        await db.Entry(@event).ReloadAsync(ct);
        return attendance;
    }

    /// <summary>Entra en la lista de espera de un evento (solo si está lleno).</summary>
    public async Task<EventAttendance> JoinWaitlist(
        Guid eventId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var @event = await db.ClubEvents.FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw new GraphQLException("No se encontró el evento.");
        await RequireActiveMembershipAsync(db, @event.ClubId, profile.Id, ct, http);

        var existing = await db.EventAttendances.FirstOrDefaultAsync(a => a.EventId == eventId && a.ProfileId == profile.Id, ct);
        if (existing is not null)
            return existing;

        if (@event.MaxAttendees is null || @event.ConfirmedCount < @event.MaxAttendees)
            throw new GraphQLException("El evento aún tiene cupos disponibles; confirma asistencia.");

        await db.ClubEvents
            .Where(e => e.Id == eventId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.WaitlistCount, e => e.WaitlistCount + 1), ct);

        var attendance = new EventAttendance
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            ProfileId = profile.Id,
            Status = EventAttendanceStatus.ListaEspera,
            CreatedAt = DateTime.UtcNow,
        };
        db.EventAttendances.Add(attendance);
        await db.SaveChangesAsync(ct);
        return attendance;
    }

    /// <summary>Check-in en un evento (requiere asistencia Confirmada previa).</summary>
    public async Task<EventAttendance> CheckIn(
        Guid eventId,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var @event = await db.ClubEvents.FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw new GraphQLException("No se encontró el evento.");
        await RequireActiveMembershipAsync(db, @event.ClubId, profile.Id, ct, http);

        var attendance = await db.EventAttendances
            .FirstOrDefaultAsync(a => a.EventId == eventId && a.ProfileId == profile.Id, ct)
            ?? throw new GraphQLException("Confirma asistencia antes del check-in.");
        if (attendance.Status == EventAttendanceStatus.ListaEspera)
            throw new GraphQLException("Estás en lista de espera; aún no tienes cupo confirmado.");

        attendance.Status = EventAttendanceStatus.CheckIn;
        await db.SaveChangesAsync(ct);
        return attendance;
    }

    // ─── LIVES ──────────────────────────────────────────────────────────

    /// <summary>Programa un live del club (solo Admin/Moderador).</summary>
    public async Task<LiveSession> ScheduleLive(
        Guid clubId,
        LiveSessionInput input,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        await RequireClubRoleAsync(db, clubId, profile.Id, ClubMemberRole.Moderador, ct, http);
        if (string.IsNullOrWhiteSpace(input.Title))
            throw new GraphQLException("El título del live es obligatorio.");

        var live = new LiveSession
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            EventId = input.EventId,
            Title = input.Title.Trim(),
            ScheduledStartAt = input.ScheduledStartAt,
            EmbedUrl = input.EmbedUrl,
            CreatedAt = DateTime.UtcNow,
        };
        db.LiveSessions.Add(live);

        if (input.Speakers is { Count: > 0 })
        {
            foreach (var speakerId in input.Speakers)
            {
                db.LiveSessionSpeakers.Add(new LiveSessionSpeaker
                {
                    LiveSessionId = live.Id,
                    ProfileId = speakerId,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return live;
    }

    /// <summary>Envía un mensaje al chat del live (requiere membresía Activa).</summary>
    public async Task<LiveChatMessage> SendLiveChatMessage(
        Guid liveId,
        string body,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        [Service] ITopicEventSender sender,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var live = await db.LiveSessions.FirstOrDefaultAsync(l => l.Id == liveId, ct)
            ?? throw new GraphQLException("No se encontró el live.");
        if (string.IsNullOrWhiteSpace(body))
            throw new GraphQLException("El mensaje no puede estar vacío.");

        await RequireActiveMembershipAsync(db, live.ClubId, profile.Id, ct, http);

        var message = new LiveChatMessage
        {
            Id = Guid.NewGuid(),
            LiveSessionId = liveId,
            SenderProfileId = profile.Id,
            Body = body.Trim(),
            SentAt = DateTime.UtcNow,
        };
        db.LiveChatMessages.Add(message);
        await db.SaveChangesAsync(ct);
        await sender.SendAsync($"live_{liveId}_chat", message, ct);
        return message;
    }

    // ─── MODERACIÓN Y NOTIFICACIONES ────────────────────────────────────

    /// <summary>Resuelve (borra el contenido) o ignora (descarta) un reporte del club.</summary>
    public async Task<bool> ResolveReport(
        Guid clubId,
        Guid reportId,
        string action,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var actor = await RequireMyProfileAsync(db, http, ct);
        await RequireClubRoleAsync(db, clubId, actor.Id, ClubMemberRole.Moderador, ct, http);

        var postReport = await db.PostReports.FirstOrDefaultAsync(r => r.Id == reportId, ct);
        if (postReport is not null)
        {
            if (!await db.Posts.AnyAsync(p => p.Id == postReport.PostId && p.ClubId == clubId, ct))
                throw new GraphQLException("El reporte no pertenece a este club.");
            db.PostReports.Remove(postReport);
            if (action.Equals("RESUELTO", StringComparison.OrdinalIgnoreCase))
            {
                var post = await db.Posts.FirstAsync(p => p.Id == postReport.PostId, ct);
                post.DeletedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            return true;
        }

        var commentReport = await db.CommentReports.FirstOrDefaultAsync(r => r.Id == reportId, ct);
        if (commentReport is not null)
        {
            var commentClub = await db.Comments.Where(c => c.Id == commentReport.CommentId).Select(c => c.Post!.ClubId).FirstOrDefaultAsync(ct);
            if (commentClub != clubId)
                throw new GraphQLException("El reporte no pertenece a este club.");
            db.CommentReports.Remove(commentReport);
            if (action.Equals("RESUELTO", StringComparison.OrdinalIgnoreCase))
            {
                var comment = await db.Comments.FirstAsync(c => c.Id == commentReport.CommentId, ct);
                comment.DeletedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            return true;
        }

        throw new GraphQLException("No se encontró el reporte.");
    }

    /// <summary>Marca una notificación como leída.</summary>
    public async Task<ClubNotification> MarkNotificationRead(
        Guid id,
        [Service] CommunityDbContext db,
        [Service] IHttpContextAccessor http,
        CancellationToken ct)
    {
        var profile = await RequireMyProfileAsync(db, http, ct);
        var notification = await db.ClubNotifications
            .FirstOrDefaultAsync(n => n.Id == id && n.ProfileId == profile.Id, ct)
            ?? throw new GraphQLException("No se encontró la notificación.");

        notification.ReadAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return notification;
    }

    // ─── HELPERS ────────────────────────────────────────────────────────

    private static async Task<Profile> RequireMyProfileAsync(CommunityDbContext db, IHttpContextAccessor http, CancellationToken ct)
    {
        // Blindaje anti-IDOR (Fase 10): sin JWT no hay identidad y no se
        // aprovisiona nada: jamás se crea un perfil con UserId nulo para un
        // solicitante anónimo.
        var userId = CommunityQuery.CurrentUserId(http)
            ?? throw new GraphQLException("Debes iniciar sesión para usar los clubes.");
        var existing = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (existing is not null)
            return existing;

        // Auto-provisión (mismo patrón que la query Me): cualquier usuario
        // autenticado obtiene su perfil de comunidad al primer uso.
        var displayName = http.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? "Miembro Copp Adresd";
        var created = new Profile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DisplayName = displayName,
            Status = ProfileStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
        db.Profiles.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    /// <summary>Exige membresía Activa (no Pendiente/Expulsado/Silenciado).
    /// Los perfiles con permiso Community.Manage (consola ERP) no requieren membresía.</summary>
    private static async Task RequireActiveMembershipAsync(CommunityDbContext db, Guid clubId, Guid profileId, CancellationToken ct, IHttpContextAccessor? http = null)
    {
        if (http is not null && HasPermission(http, "Community.Manage"))
            return;

        var member = await db.ClubMembers.FirstOrDefaultAsync(m => m.ClubId == clubId && m.ProfileId == profileId, ct)
            ?? throw new GraphQLException("Debes ser miembro del club para esta acción.");
        if (member.Status == ClubMemberStatus.Expulsado)
            throw new GraphQLException("Fuiste expulsado de este club.");
        if (member.Status == ClubMemberStatus.Silenciado && (member.MutedUntil is null || member.MutedUntil > DateTime.UtcNow))
            throw new GraphQLException("Estás silenciado en este club.");
        if (member.Status == ClubMemberStatus.Pendiente)
            throw new GraphQLException("Tu solicitud de ingreso aún está pendiente.");
    }

    /// <summary>Exige un rol mínimo del solicitante dentro del club.
    /// Los perfiles con permiso Community.Manage (consola ERP) gestionan cualquier club.</summary>
    private static async Task RequireClubRoleAsync(CommunityDbContext db, Guid clubId, Guid profileId, ClubMemberRole minRole, CancellationToken ct, IHttpContextAccessor? http = null)
    {
        if (http is not null && HasPermission(http, "Community.Manage"))
            return;

        var member = await db.ClubMembers.FirstOrDefaultAsync(m => m.ClubId == clubId && m.ProfileId == profileId && m.Status == ClubMemberStatus.Activo, ct)
            ?? throw new GraphQLException("Debes ser miembro activo del club para esta acción.");

        var allowed = minRole switch
        {
            ClubMemberRole.Admin => member.Role == ClubMemberRole.Admin,
            ClubMemberRole.Moderador => member.Role is ClubMemberRole.Admin or ClubMemberRole.Moderador,
            _ => true,
        };
        if (!allowed)
            throw new GraphQLException("No tienes permisos para esta acción en el club.");
    }

    /// <summary>True si el JWT del solicitante incluye el permiso indicado (claims "permission").</summary>
    private static bool HasPermission(IHttpContextAccessor http, string permissionCode)
        => http.HttpContext?.User?.Claims.Any(c =>
            c.Type == "permission" && c.Value.Equals(permissionCode, StringComparison.OrdinalIgnoreCase)) == true;

    private static async Task<ClubMember> CreateOrReactivateMembershipAsync(
        CommunityDbContext db, Club club, Profile profile, ClubMemberStatus status, ClubMemberRole role,
        ITopicEventSender sender, CancellationToken ct)
    {
        var existing = await db.ClubMembers.FirstOrDefaultAsync(m => m.ClubId == club.Id && m.ProfileId == profile.Id, ct);
        if (existing is not null)
        {
            if (existing.Status == ClubMemberStatus.Expulsado)
                throw new GraphQLException("Fuiste expulsado de este club.");
            if (existing.Status == ClubMemberStatus.Pendiente) return existing;
            // Reactivar no degrada roles de gestión (Admin/Moderador se conservan).
            if (existing.Role == ClubMemberRole.Miembro)
            {
                existing.Role = role;
            }
            existing.Status = ClubMemberStatus.Activo;
            existing.JoinedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await sender.SendAsync($"club_member_{profile.Id}", existing, ct);
            return existing;
        }

        var member = new ClubMember
        {
            ClubId = club.Id,
            ProfileId = profile.Id,
            Role = role,
            Status = status,
            JoinedAt = DateTime.UtcNow,
        };
        db.ClubMembers.Add(member);
        await db.SaveChangesAsync(ct);
        await sender.SendAsync($"club_member_{profile.Id}", member, ct);
        return member;
    }

    private static async Task ConfirmAtomicAsync(CommunityDbContext db, ClubEvent @event, CancellationToken ct)
    {
        await db.ClubEvents
            .Where(e => e.Id == @event.Id && (e.MaxAttendees == null || e.ConfirmedCount < e.MaxAttendees))
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.ConfirmedCount, e => e.ConfirmedCount + 1)
                .SetProperty(e => e.Status, e => ClubEventStatus.Abierto), ct);
    }

    private static string Slugify(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        var builder = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }
        return builder.ToString().Trim('-');
    }
}