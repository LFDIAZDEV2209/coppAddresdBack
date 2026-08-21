using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

public sealed class LikeConfiguration : IEntityTypeConfiguration<Like>
{
    public void Configure(EntityTypeBuilder<Like> builder)
    {
        builder.ToTable("likes", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(x => x.PostId).HasColumnName("post_id");
        builder.Property(x => x.CommentId).HasColumnName("comment_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Profile)
            .WithMany(p => p.Likes)
            .HasForeignKey(x => x.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Post)
            .WithMany(p => p.Likes)
            .HasForeignKey(x => x.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Comment)
            .WithMany(c => c.Likes)
            .HasForeignKey(x => x.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un usuario no puede dar like dos veces a lo mismo.
        builder.HasIndex(x => new { x.PostId, x.ProfileId })
            .IsUnique()
            .HasDatabaseName("ix_likes_post_profile");
        builder.HasIndex(x => new { x.CommentId, x.ProfileId })
            .IsUnique()
            .HasDatabaseName("ix_likes_comment_profile");
        builder.HasIndex(x => x.PostId).HasDatabaseName("ix_likes_post_id");
        builder.HasIndex(x => x.CommentId).HasDatabaseName("ix_likes_comment_id");
    }
}
