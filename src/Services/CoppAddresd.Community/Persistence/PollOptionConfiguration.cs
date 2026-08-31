using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

public sealed class PollOptionConfiguration : IEntityTypeConfiguration<PollOption>
{
    public void Configure(EntityTypeBuilder<PollOption> builder)
    {
        builder.ToTable("poll_options", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.PollId).HasColumnName("poll_id").IsRequired();
        builder.Property(x => x.Text).HasColumnName("text").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Position).HasColumnName("position").IsRequired();

        builder.HasOne(x => x.Poll)
            .WithMany(p => p.Options)
            .HasForeignKey(x => x.PollId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Votes)
            .WithOne(v => v.Option)
            .HasForeignKey(v => v.OptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.PollId, x.Position }).IsUnique().HasDatabaseName("ux_poll_options_poll_position");
    }
}
