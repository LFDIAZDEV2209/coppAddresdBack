using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

/// <summary>Configuración de la tabla community.live_chat_messages.</summary>
public sealed class LiveChatMessageConfiguration : IEntityTypeConfiguration<LiveChatMessage>
{
    public void Configure(EntityTypeBuilder<LiveChatMessage> builder)
    {
        builder.ToTable("live_chat_messages", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.LiveSessionId).HasColumnName("live_session_id").IsRequired();
        builder.Property(x => x.SenderProfileId).HasColumnName("sender_profile_id").IsRequired();
        builder.Property(x => x.Body).HasColumnName("body").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.SentAt).HasColumnName("sent_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.LiveSession).WithMany(l => l.ChatMessages).HasForeignKey(x => x.LiveSessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.SenderProfile).WithMany().HasForeignKey(x => x.SenderProfileId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.LiveSessionId, x.SentAt }).HasDatabaseName("ix_live_chat_messages_live_session_id_sent_at");
    }
}