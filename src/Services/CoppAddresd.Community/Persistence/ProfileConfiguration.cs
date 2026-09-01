using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CoppAddresd.Community.Persistence;

public sealed class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    private static readonly ValueConverter<ProfileRegion?, string?> SafeRegionConverter = new(
        v => v.HasValue ? v.Value.ToString() : null,
        v => SafeParseRegion(v));

    private static ProfileRegion? SafeParseRegion(string? v)
    {
        if (v == null) return null;
        return Enum.TryParse<ProfileRegion>(v, ignoreCase: true, out var result) ? result : null;
    }

    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.ToTable("profiles", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.IsSystem).HasColumnName("is_system").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Bio).HasColumnName("bio").HasMaxLength(500);
        builder.Property(x => x.AvatarKey).HasColumnName("avatar_key").HasMaxLength(500);
        builder.Property(x => x.CoverKey).HasColumnName("cover_key").HasMaxLength(500);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.BannedBy).HasColumnName("banned_by");
        builder.Property(x => x.BannedAt).HasColumnName("banned_at").HasColumnType("timestamptz");
        builder.Property(x => x.BanReason).HasColumnName("ban_reason").HasMaxLength(300);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        builder.Property(x => x.Region).HasColumnName("region").HasMaxLength(30).HasConversion(SafeRegionConverter);
        builder.Property(x => x.Diagnosis).HasColumnName("diagnosis").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Week).HasColumnName("week");
        builder.Property(x => x.LastPostAt).HasColumnName("last_post_at").HasColumnType("timestamptz");
        builder.Property(x => x.LastActiveAt).HasColumnName("last_active_at").HasColumnType("timestamptz");
        builder.Property(x => x.CurrentStreak).HasColumnName("current_streak").IsRequired();
        builder.Property(x => x.BestStreak).HasColumnName("best_streak").IsRequired();
        builder.Property(x => x.XpTotal).HasColumnName("xp_total").IsRequired();

        builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("ix_profiles_user_id");
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_profiles_status");
        builder.HasIndex(x => x.Region).HasDatabaseName("ix_profiles_region");
    }
}
