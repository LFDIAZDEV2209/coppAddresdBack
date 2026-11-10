using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.Persistence;

/// <summary>
/// DbContext del microservicio de comunidad. Usa el schema <c>community</c> y
/// su propio historial de migraciones (<c>community.__EFMigrationsHistory</c>),
/// aislado del historial compartido <c>public.__EFMigrationsHistory</c>.
/// </summary>
public sealed class CommunityDbContext(
    DbContextOptions<CommunityDbContext> options,
    IHttpContextAccessor? http = null) : DbContext(options)
{
    public const string VisibilityProfileItem = "community.blocking.viewer";
    private Guid? visibilityOverride;

    /// <summary>Vacío para procesos de fondo y consultas ERP; nunca se infiere un perfil de sistema.</summary>
    public Guid VisibilityProfileId => visibilityOverride
        ?? (http?.HttpContext?.Items[VisibilityProfileItem] is Guid id ? id : Guid.Empty);
    public bool BlockFilteringEnabled => VisibilityProfileId != Guid.Empty;

    public void UseBlockVisibility(Guid? profileId) => visibilityOverride = profileId ?? Guid.Empty;

    /// <summary>Función SQL estable: verifica la raíz del hilo sin recursión de filtros EF.</summary>
    public static bool IsCommentThreadVisible(Guid viewerProfileId, Guid rootCommentId)
        => throw new InvalidOperationException("Esta función solo se ejecuta en PostgreSQL.");

    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<ProfileBlock> ProfileBlocks => Set<ProfileBlock>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ChatGroup> ChatGroups => Set<ChatGroup>();
    public DbSet<ChatGroupMember> ChatGroupMembers => Set<ChatGroupMember>();
    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<PollOption> PollOptions => Set<PollOption>();
    public DbSet<PollVote> PollVotes => Set<PollVote>();
    public DbSet<XpEntry> XpEntries => Set<XpEntry>();
    public DbSet<FeedEvent> FeedEvents => Set<FeedEvent>();
    public DbSet<Recognition> Recognitions => Set<Recognition>();
    public DbSet<NetworkChannel> NetworkChannels => Set<NetworkChannel>();
    public DbSet<NetworkGrowthPoint> NetworkGrowthPoints => Set<NetworkGrowthPoint>();
    public DbSet<PostReport> PostReports => Set<PostReport>();
    public DbSet<CommentReport> CommentReports => Set<CommentReport>();
    public DbSet<Repost> Reposts => Set<Repost>();
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<ClubMember> ClubMembers => Set<ClubMember>();
    public DbSet<ClubInvitation> ClubInvitations => Set<ClubInvitation>();
    public DbSet<ClubCategory> ClubCategories => Set<ClubCategory>();
    public DbSet<ClubEvent> ClubEvents => Set<ClubEvent>();
    public DbSet<EventAttendance> EventAttendances => Set<EventAttendance>();
    public DbSet<LiveSession> LiveSessions => Set<LiveSession>();
    public DbSet<LiveChatMessage> LiveChatMessages => Set<LiveChatMessage>();
    public DbSet<LiveSessionSpeaker> LiveSessionSpeakers => Set<LiveSessionSpeaker>();
    public DbSet<ClubNotification> ClubNotifications => Set<ClubNotification>();
    public DbSet<ModerationLog> ModerationLogs => Set<ModerationLog>();
    public DbSet<CommunityDailyMetric> CommunityDailyMetrics => Set<CommunityDailyMetric>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunityDbContext).Assembly);
        modelBuilder.HasDbFunction(typeof(CommunityDbContext).GetMethod(nameof(IsCommentThreadVisible))!)
            .HasName("is_comment_thread_visible").HasSchema("community");

        // Solo se activan con un visor autenticado de la app. ERP y trabajos de
        // moderación conservan las pruebas completas; los SQL manuales filtran antes de paginar.
        modelBuilder.Entity<Profile>().HasQueryFilter(x => !BlockFilteringEnabled || !ProfileBlocks.Any(b =>
            (b.BlockerProfileId == VisibilityProfileId && b.BlockedProfileId == x.Id)
            || (b.BlockedProfileId == VisibilityProfileId && b.BlockerProfileId == x.Id)));
        modelBuilder.Entity<Post>().HasQueryFilter(x => !BlockFilteringEnabled || !ProfileBlocks.Any(b =>
            (b.BlockerProfileId == VisibilityProfileId && b.BlockedProfileId == x.ProfileId)
            || (b.BlockedProfileId == VisibilityProfileId && b.BlockerProfileId == x.ProfileId)));
        modelBuilder.Entity<Comment>().HasQueryFilter(x => !BlockFilteringEnabled ||
            (!ProfileBlocks.Any(b =>
                (b.BlockerProfileId == VisibilityProfileId && (b.BlockedProfileId == x.ProfileId || b.BlockedProfileId == x.Post!.ProfileId))
                || (b.BlockedProfileId == VisibilityProfileId && (b.BlockerProfileId == x.ProfileId || b.BlockerProfileId == x.Post!.ProfileId)))
             && (x.ParentCommentId == null || IsCommentThreadVisible(VisibilityProfileId, x.ParentCommentId.Value))));
        modelBuilder.Entity<Like>().HasQueryFilter(x => !BlockFilteringEnabled ||
            (!ProfileBlocks.Any(b =>
                (b.BlockerProfileId == VisibilityProfileId && (b.BlockedProfileId == x.ProfileId
                    || (x.PostId != null && b.BlockedProfileId == x.Post!.ProfileId)
                    || (x.CommentId != null && (b.BlockedProfileId == x.Comment!.ProfileId || b.BlockedProfileId == x.Comment.Post!.ProfileId))))
                || (b.BlockedProfileId == VisibilityProfileId && (b.BlockerProfileId == x.ProfileId
                    || (x.PostId != null && b.BlockerProfileId == x.Post!.ProfileId)
                    || (x.CommentId != null && (b.BlockerProfileId == x.Comment!.ProfileId || b.BlockerProfileId == x.Comment.Post!.ProfileId)))))
             && (x.CommentId == null || x.Comment!.ParentCommentId == null
                 || IsCommentThreadVisible(VisibilityProfileId, x.Comment.ParentCommentId.Value))));
        modelBuilder.Entity<Repost>().HasQueryFilter(x => !BlockFilteringEnabled ||
            !ProfileBlocks.Any(b =>
                (b.BlockerProfileId == VisibilityProfileId && (b.BlockedProfileId == x.ProfileId || b.BlockedProfileId == x.Post!.ProfileId))
                || (b.BlockedProfileId == VisibilityProfileId && (b.BlockerProfileId == x.ProfileId || b.BlockerProfileId == x.Post!.ProfileId))));
        modelBuilder.Entity<Follow>().HasQueryFilter(x => !BlockFilteringEnabled ||
            !ProfileBlocks.Any(b =>
                (b.BlockerProfileId == VisibilityProfileId && (b.BlockedProfileId == x.FollowerProfileId || b.BlockedProfileId == x.FollowingProfileId))
                || (b.BlockedProfileId == VisibilityProfileId && (b.BlockerProfileId == x.FollowerProfileId || b.BlockerProfileId == x.FollowingProfileId))));
        modelBuilder.Entity<Message>().HasQueryFilter(x => !BlockFilteringEnabled ||
            !ProfileBlocks.Any(b =>
                (b.BlockerProfileId == VisibilityProfileId && (b.BlockedProfileId == x.SenderProfileId || b.BlockedProfileId == x.RecipientProfileId))
                || (b.BlockedProfileId == VisibilityProfileId && (b.BlockerProfileId == x.SenderProfileId || b.BlockerProfileId == x.RecipientProfileId))));
        modelBuilder.Entity<ChatGroupMember>().HasQueryFilter(x => !BlockFilteringEnabled || !ProfileBlocks.Any(b =>
            (b.BlockerProfileId == VisibilityProfileId && b.BlockedProfileId == x.ProfileId)
            || (b.BlockedProfileId == VisibilityProfileId && b.BlockerProfileId == x.ProfileId)));
        modelBuilder.Entity<PollVote>().HasQueryFilter(x => !BlockFilteringEnabled || !ProfileBlocks.Any(b =>
            (b.BlockerProfileId == VisibilityProfileId && b.BlockedProfileId == x.ProfileId)
            || (b.BlockedProfileId == VisibilityProfileId && b.BlockerProfileId == x.ProfileId)));
        modelBuilder.Entity<FeedEvent>().HasQueryFilter(x => !BlockFilteringEnabled || !ProfileBlocks.Any(b =>
            (b.BlockerProfileId == VisibilityProfileId && b.BlockedProfileId == x.ProfileId)
            || (b.BlockedProfileId == VisibilityProfileId && b.BlockerProfileId == x.ProfileId)));
        modelBuilder.Entity<Recognition>().HasQueryFilter(x => !BlockFilteringEnabled || !ProfileBlocks.Any(b =>
            (b.BlockerProfileId == VisibilityProfileId && b.BlockedProfileId == x.ProfileId)
            || (b.BlockedProfileId == VisibilityProfileId && b.BlockerProfileId == x.ProfileId)));
        modelBuilder.Entity<ClubMember>().HasQueryFilter(x => !BlockFilteringEnabled || !ProfileBlocks.Any(b =>
            (b.BlockerProfileId == VisibilityProfileId && b.BlockedProfileId == x.ProfileId)
            || (b.BlockedProfileId == VisibilityProfileId && b.BlockerProfileId == x.ProfileId)));
        modelBuilder.Entity<LiveChatMessage>().HasQueryFilter(x => !BlockFilteringEnabled || !ProfileBlocks.Any(b =>
            (b.BlockerProfileId == VisibilityProfileId && b.BlockedProfileId == x.SenderProfileId)
            || (b.BlockedProfileId == VisibilityProfileId && b.BlockerProfileId == x.SenderProfileId)));
        base.OnModelCreating(modelBuilder);
    }
}
