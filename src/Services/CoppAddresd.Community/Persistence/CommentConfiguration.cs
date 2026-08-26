using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.PostId).HasColumnName("post_id").IsRequired();
        builder.Property(x => x.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(x => x.ParentCommentId).HasColumnName("parent_comment_id");
        builder.Property(x => x.Body).HasColumnName("body").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamptz");

        builder.HasOne(x => x.Post)
            .WithMany(p => p.Comments)
            .HasForeignKey(x => x.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Profile)
            .WithMany(p => p.Comments)
            .HasForeignKey(x => x.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Auto-relación: respuestas a un comentario (ParentCommentId → Replies).
        builder.HasOne<Comment>()
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PostId).HasDatabaseName("ix_comments_post_id");
        builder.HasIndex(x => x.ParentCommentId).HasDatabaseName("ix_comments_parent_comment_id");
    }
}
