using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

/// <summary>Configuración de la tabla community.club_categories (catálogo admin).</summary>
public sealed class ClubCategoryConfiguration : IEntityTypeConfiguration<ClubCategory>
{
    public void Configure(EntityTypeBuilder<ClubCategory> builder)
    {
        builder.ToTable("club_categories", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(60).IsRequired();
        builder.Property(x => x.Icon).HasColumnName("icon").HasMaxLength(60);

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("ix_club_categories_slug");
    }
}