using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.Persistence;

/// <summary>
/// DbContext del microservicio de comunidad. Usa el schema <c>community</c> y
/// su propio historial de migraciones (<c>community.__EFMigrationsHistory</c>),
/// aislado del historial compartido <c>public.__EFMigrationsHistory</c>.
/// </summary>
public sealed class CommunityDbContext(DbContextOptions<CommunityDbContext> options) : DbContext(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<Follow> Follows => Set<Follow>();
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
        base.OnModelCreating(modelBuilder);
    }
}
