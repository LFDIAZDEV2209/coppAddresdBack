using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

public sealed class ProfileBlockConfiguration : IEntityTypeConfiguration<ProfileBlock>
{
    public void Configure(EntityTypeBuilder<ProfileBlock> builder)
    {
        builder.ToTable("profile_blocks", "community", table =>
            table.HasCheckConstraint("ck_profile_blocks_not_self", "blocker_profile_id <> blocked_profile_id"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.BlockerProfileId).HasColumnName("blocker_profile_id").IsRequired();
        builder.Property(x => x.BlockedProfileId).HasColumnName("blocked_profile_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz")
            .HasDefaultValueSql("now()").IsRequired();
        builder.HasOne<Profile>().WithMany().HasForeignKey(x => x.BlockerProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Profile>().WithMany().HasForeignKey(x => x.BlockedProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.BlockerProfileId, x.BlockedProfileId }).IsUnique()
            .HasDatabaseName("ix_profile_blocks_blocker_blocked");
        builder.HasIndex(x => x.BlockedProfileId).HasDatabaseName("ix_profile_blocks_blocked");
    }
}
