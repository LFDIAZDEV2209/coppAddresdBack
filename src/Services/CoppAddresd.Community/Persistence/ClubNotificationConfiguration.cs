using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

/// <summary>Configuración de la tabla community.club_notifications.</summary>
public sealed class ClubNotificationConfiguration : IEntityTypeConfiguration<ClubNotification>
{
    public void Configure(EntityTypeBuilder<ClubNotification> builder)
    {
        builder.ToTable("club_notifications", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ClubId).HasColumnName("club_id").IsRequired();
        builder.Property(x => x.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("text");
        builder.Property(x => x.ReadAt).HasColumnName("read_at").HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Club).WithMany(c => c.Notifications).HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Profile).WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ProfileId, x.ReadAt }).HasDatabaseName("ix_club_notifications_profile_id_read_at");
        builder.HasIndex(x => x.ClubId).HasDatabaseName("ix_club_notifications_club_id");
    }
}