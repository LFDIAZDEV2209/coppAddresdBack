using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

/// <summary>Configuración de la tabla community.live_session_speakers (PK compuesta).</summary>
public sealed class LiveSessionSpeakerConfiguration : IEntityTypeConfiguration<LiveSessionSpeaker>
{
    public void Configure(EntityTypeBuilder<LiveSessionSpeaker> builder)
    {
        builder.ToTable("live_session_speakers", "community");
        builder.HasKey(x => new { x.LiveSessionId, x.ProfileId });
        builder.Property(x => x.LiveSessionId).HasColumnName("live_session_id").IsRequired();
        builder.Property(x => x.ProfileId).HasColumnName("profile_id").IsRequired();

        builder.HasOne(x => x.LiveSession).WithMany(l => l.Speakers).HasForeignKey(x => x.LiveSessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Profile).WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}