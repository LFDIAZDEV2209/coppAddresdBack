using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

/// <summary>Configuración de la tabla community.clubs.</summary>
public sealed class ClubConfiguration : IEntityTypeConfiguration<Club>
{
    public void Configure(EntityTypeBuilder<Club> builder)
    {
        builder.ToTable("clubs", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Rules).HasColumnName("rules").HasColumnType("text[]").IsRequired();
        builder.Property(x => x.Objectives).HasColumnName("objectives").HasColumnType("text[]").IsRequired();
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(60).IsRequired();
        builder.Property(x => x.Tags).HasColumnName("tags").HasColumnType("text[]").IsRequired();
        builder.Property(x => x.CoverKey).HasColumnName("cover_key").HasMaxLength(512);
        builder.Property(x => x.LogoKey).HasColumnName("logo_key").HasMaxLength(512);
        builder.Property(x => x.Visibility).HasColumnName("visibility").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.MaxMembers).HasColumnName("max_members");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedByProfileId).HasColumnName("created_by_profile_id").IsRequired();
        builder.Property(x => x.IsSystem).HasColumnName("is_system").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        builder.HasOne(x => x.CreatedByProfile)
            .WithMany()
            .HasForeignKey(x => x.CreatedByProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("ix_clubs_slug");
        builder.HasIndex(x => x.Category).HasDatabaseName("ix_clubs_category");
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_clubs_status");
    }
}