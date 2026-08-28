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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
