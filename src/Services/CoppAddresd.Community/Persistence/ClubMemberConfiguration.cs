using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

/// <summary>
/// Configuración de la tabla community.club_members (PK compuesta club + perfil,
/// membresía única por pareja).
/// </summary>
public sealed class ClubMemberConfiguration : IEntityTypeConfiguration<ClubMember>
{
    public void Configure(EntityTypeBuilder<ClubMember> builder)
    {
        builder.ToTable("club_members", "community");
        builder.HasKey(x => new { x.ClubId, x.ProfileId });
        builder.Property(x => x.ClubId).HasColumnName("club_id").IsRequired();
        builder.Property(x => x.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.MutedUntil).HasColumnName("muted_until").HasColumnType("timestamptz");
        builder.Property(x => x.JoinedAt).HasColumnName("joined_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Club).WithMany(c => c.Members).HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Profile).WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ClubId, x.Role }).HasDatabaseName("ix_club_members_club_id_role");
        builder.HasIndex(x => x.ProfileId).HasDatabaseName("ix_club_members_profile_id");
    }
}