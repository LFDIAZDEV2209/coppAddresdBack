using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

public sealed class XpEntryConfiguration : IEntityTypeConfiguration<XpEntry>
{
    public void Configure(EntityTypeBuilder<XpEntry> builder)
    {
        builder.ToTable("xp_entries", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(300);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Profile)
            .WithMany(p => p.XpEntries)
            .HasForeignKey(x => x.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ProfileId).HasDatabaseName("ix_xp_entries_profile_id");
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_xp_entries_created_at");
    }
}
