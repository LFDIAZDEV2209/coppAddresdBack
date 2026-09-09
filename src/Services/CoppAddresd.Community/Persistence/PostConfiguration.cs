using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

public sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("posts", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(x => x.Body).HasColumnName("body").HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ImageKey).HasColumnName("image_key").HasMaxLength(512);
        builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Destination).HasColumnName("destination").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ClubId).HasColumnName("club_id");
        builder.Property(x => x.ClubStatus).HasColumnName("club_status").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ClubVisibility).HasColumnName("club_visibility").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Featured).HasColumnName("featured").IsRequired();
        builder.Property(x => x.ScheduledFor).HasColumnName("scheduled_for").HasColumnType("timestamptz");
        builder.Property(x => x.ViewCount).HasColumnName("view_count").IsRequired();
        builder.Property(x => x.Pinned).HasColumnName("pinned").IsRequired();
        builder.Property(x => x.PinnedOrder).HasColumnName("pinned_order").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamptz");

        builder.HasOne(x => x.Profile)
            .WithMany(p => p.Posts)
            .HasForeignKey(x => x.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Club)
            .WithMany()
            .HasForeignKey(x => x.ClubId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProfileId).HasDatabaseName("ix_posts_profile_id");
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_posts_created_at");
        // Keyset pagination del feed por club + filtro por estado.
        builder.HasIndex(x => new { x.ClubId, x.CreatedAt }).HasDatabaseName("ix_posts_club_id_created_at");
        builder.HasIndex(x => new { x.ClubId, x.ClubStatus }).HasDatabaseName("ix_posts_club_id_club_status");
    }
}
