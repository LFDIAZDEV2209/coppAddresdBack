using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

/// <summary>Configuración de la tabla community.reposts.</summary>
public sealed class RepostConfiguration : IEntityTypeConfiguration<Repost>
{
    public void Configure(EntityTypeBuilder<Repost> builder)
    {
        builder.ToTable("reposts", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.PostId).HasColumnName("post_id").IsRequired();
        builder.Property(x => x.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Post)
            .WithMany(p => p.Reposts)
            .HasForeignKey(x => x.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Profile)
            .WithMany()
            .HasForeignKey(x => x.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un usuario no puede repostear la misma publicación dos veces.
        builder.HasIndex(x => new { x.PostId, x.ProfileId })
            .IsUnique()
            .HasDatabaseName("ix_reposts_post_profile");
        builder.HasIndex(x => x.PostId).HasDatabaseName("ix_reposts_post_id");
        builder.HasIndex(x => x.ProfileId).HasDatabaseName("ix_reposts_profile_id");
    }
}
