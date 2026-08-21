using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.SenderProfileId).HasColumnName("sender_profile_id").IsRequired();
        builder.Property(x => x.RecipientProfileId).HasColumnName("recipient_profile_id").IsRequired();
        builder.Property(x => x.Body).HasColumnName("body").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne<Profile>().WithMany().HasForeignKey(x => x.SenderProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Profile>().WithMany().HasForeignKey(x => x.RecipientProfileId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SenderProfileId, x.RecipientProfileId, x.CreatedAt }).IsDescending(false, false, true).HasDatabaseName("ix_messages_sender_recipient_created");
    }
}
