using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

/// <summary>Configuración de la tabla community.club_events.</summary>
public sealed class ClubEventConfiguration : IEntityTypeConfiguration<ClubEvent>
{
    public void Configure(EntityTypeBuilder<ClubEvent> builder)
    {
        builder.ToTable("club_events", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ClubId).HasColumnName("club_id").IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.StartsAt).HasColumnName("starts_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.EndsAt).HasColumnName("ends_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Location).HasColumnName("location").HasMaxLength(255);
        builder.Property(x => x.MeetingUrl).HasColumnName("meeting_url").HasMaxLength(512);
        builder.Property(x => x.MaxAttendees).HasColumnName("max_attendees");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ConfirmedCount).HasColumnName("confirmed_count").IsRequired();
        builder.Property(x => x.WaitlistCount).HasColumnName("waitlist_count").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Club).WithMany(c => c.Events).HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ClubId, x.StartsAt }).HasDatabaseName("ix_club_events_club_id_starts_at");
    }
}