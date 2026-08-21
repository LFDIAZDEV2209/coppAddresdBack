using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

public sealed class FollowConfiguration : IEntityTypeConfiguration<Follow>
{
    public void Configure(EntityTypeBuilder<Follow> builder)
    {
        builder.ToTable("follows", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.FollowerProfileId).HasColumnName("follower_profile_id").IsRequired();
        builder.Property(x => x.FollowingProfileId).HasColumnName("following_profile_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne<Profile>().WithMany().HasForeignKey(x => x.FollowerProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Profile>().WithMany().HasForeignKey(x => x.FollowingProfileId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.FollowerProfileId, x.FollowingProfileId }).IsUnique().HasDatabaseName("ix_follows_follower_following");
        builder.HasIndex(x => x.FollowingProfileId).HasDatabaseName("ix_follows_following");
    }
}
