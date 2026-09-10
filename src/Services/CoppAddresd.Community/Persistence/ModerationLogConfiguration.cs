using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

/// <summary>Configuración de la tabla community.moderation_logs (auditoría de moderación).</summary>
public sealed class ModerationLogConfiguration : IEntityTypeConfiguration<ModerationLog>
{
    public void Configure(EntityTypeBuilder<ModerationLog> builder)
    {
        builder.ToTable("moderation_logs", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ClubId).HasColumnName("club_id").IsRequired();
        builder.Property(x => x.ActorProfileId).HasColumnName("actor_profile_id").IsRequired();
        builder.Property(x => x.TargetProfileId).HasColumnName("target_profile_id").IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(60).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Club).WithMany(c => c.ModerationLogs).HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ActorProfile).WithMany().HasForeignKey(x => x.ActorProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TargetProfile).WithMany().HasForeignKey(x => x.TargetProfileId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ClubId, x.CreatedAt }).HasDatabaseName("ix_moderation_logs_club_id_created_at");
    }
}