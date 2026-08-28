using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

public sealed class FeedEventConfiguration : IEntityTypeConfiguration<FeedEvent>
{
    public void Configure(EntityTypeBuilder<FeedEvent> builder)
    {
        builder.ToTable("feed_events", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProfileId).HasColumnName("profile_id");
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Body).HasColumnName("body").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Profile)
            .WithMany()
            .HasForeignKey(x => x.ProfileId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.ProfileId).HasDatabaseName("ix_feed_events_profile_id");
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_feed_events_created_at");
    }
}
