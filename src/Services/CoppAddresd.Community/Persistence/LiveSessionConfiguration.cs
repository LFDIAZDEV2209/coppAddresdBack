using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

/// <summary>Configuración de la tabla community.live_sessions.</summary>
public sealed class LiveSessionConfiguration : IEntityTypeConfiguration<LiveSession>
{
    public void Configure(EntityTypeBuilder<LiveSession> builder)
    {
        builder.ToTable("live_sessions", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ClubId).HasColumnName("club_id").IsRequired();
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ScheduledStartAt).HasColumnName("scheduled_start_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.EmbedUrl).HasColumnName("embed_url").HasMaxLength(512);
        builder.Property(x => x.ProviderRoom).HasColumnName("provider_room").HasMaxLength(255);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Club).WithMany(c => c.LiveSessions).HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.ClubId, x.ScheduledStartAt }).HasDatabaseName("ix_live_sessions_club_id_scheduled_start_at");
    }
}