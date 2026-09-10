using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

/// <summary>Configuración de la tabla community.club_invitations (token único).</summary>
public sealed class ClubInvitationConfiguration : IEntityTypeConfiguration<ClubInvitation>
{
    public void Configure(EntityTypeBuilder<ClubInvitation> builder)
    {
        builder.ToTable("club_invitations", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ClubId).HasColumnName("club_id").IsRequired();
        builder.Property(x => x.ProfileId).HasColumnName("profile_id");
        builder.Property(x => x.Token).HasColumnName("token").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UsedAt).HasColumnName("used_at").HasColumnType("timestamptz");
        builder.Property(x => x.CreatedByProfileId).HasColumnName("created_by_profile_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Club).WithMany(c => c.Invitations).HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Token).IsUnique().HasDatabaseName("ix_club_invitations_token");
        builder.HasIndex(x => x.ClubId).HasDatabaseName("ix_club_invitations_club_id");
    }
}