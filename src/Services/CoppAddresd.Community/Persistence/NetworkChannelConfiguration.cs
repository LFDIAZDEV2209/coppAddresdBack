using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

/// <summary>Configuración de la tabla community.network_channels.</summary>
public sealed class NetworkChannelConfiguration : IEntityTypeConfiguration<NetworkChannel>
{
    public void Configure(EntityTypeBuilder<NetworkChannel> builder)
    {
        builder.ToTable("network_channels", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Handle).HasColumnName("handle").HasMaxLength(100);
        builder.Property(x => x.Followers).HasColumnName("followers").IsRequired();
        builder.Property(x => x.Color).HasColumnName("color").HasMaxLength(20).IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();

        builder.HasIndex(x => x.SortOrder).HasDatabaseName("ix_network_channels_sort_order");
    }
}

/// <summary>Configuración de la tabla community.network_growth_points.</summary>
public sealed class NetworkGrowthPointConfiguration : IEntityTypeConfiguration<NetworkGrowthPoint>
{
    public void Configure(EntityTypeBuilder<NetworkGrowthPoint> builder)
    {
        builder.ToTable("network_growth_points", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ChannelId).HasColumnName("channel_id").IsRequired();
        builder.Property(x => x.Month).HasColumnName("month").HasMaxLength(7).IsRequired();
        builder.Property(x => x.Value).HasColumnName("value").IsRequired();

        builder.HasOne(x => x.Channel)
            .WithMany(c => c.GrowthPoints)
            .HasForeignKey(x => x.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ChannelId, x.Month }).IsUnique().HasDatabaseName("ix_network_growth_points_channel_month");
    }
}
