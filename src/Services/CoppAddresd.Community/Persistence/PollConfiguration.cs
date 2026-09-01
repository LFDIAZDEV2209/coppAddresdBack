using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

public sealed class PollConfiguration : IEntityTypeConfiguration<Poll>
{
    public void Configure(EntityTypeBuilder<Poll> builder)
    {
        builder.ToTable("polls", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.PostId).HasColumnName("post_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Post)
            .WithOne(p => p.Poll)
            .HasForeignKey<Poll>(x => x.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.PostId).IsUnique().HasDatabaseName("ux_polls_post_id");

        builder.HasMany(x => x.Options)
            .WithOne(o => o.Poll)
            .HasForeignKey(o => o.PollId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
