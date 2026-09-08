using System.Text.Json;
using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración de <see cref="MediaItem"/>. Tabla de negocio en schema <c>public</c>.
/// </summary>
public sealed class MediaItemConfiguration : IEntityTypeConfiguration<MediaItem>
{
    public void Configure(EntityTypeBuilder<MediaItem> builder)
    {
        builder.ToTable("media_items", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(x => x.Author)
            .HasColumnName("author")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.MediaType)
            .HasColumnName("media_type")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.StorageKey)
            .HasColumnName("storage_key")
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(x => x.ThumbnailKey)
            .HasColumnName("thumbnail_key")
            .HasMaxLength(1024);

        builder.Property(x => x.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(100);

        builder.Property(x => x.FileSizeBytes)
            .HasColumnName("file_size_bytes");

        builder.Property(x => x.DurationSecs)
            .HasColumnName("duration_secs");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(x => x.Day)
            .HasColumnName("day");

        builder.Property(x => x.Month)
            .HasColumnName("month");

        builder.Property(x => x.PublishedAt)
            .HasColumnName("published_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(x => x.Chapters)
            .HasColumnName("chapters")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<MediaChapterDto>>(v, (JsonSerializerOptions?)null) ?? new List<MediaChapterDto>())
            .HasDefaultValueSql("'[]'::jsonb");

        builder.Property(x => x.Takeaways)
            .HasColumnName("takeaways")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .HasDefaultValueSql("'[]'::jsonb");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_media_items_status");

        builder.HasIndex(x => new { x.MediaType, x.Status })
            .HasDatabaseName("ix_media_items_type_status");

        builder.HasIndex(x => new { x.Month, x.Day })
            .HasDatabaseName("ix_media_items_month_day");
    }
}
