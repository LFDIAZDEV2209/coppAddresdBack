using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración de <c>app.notification_dedupe_keys</c> (F2): el índice único
/// de <c>dedupe_key</c> es la garantía de deduplicación; los estados por canal
/// permiten reintentar solo lo que no quedó <c>sent</c>. Sin FK a
/// <c>auth.users</c> (schema ajeno, igual que device_tokens: la relación es por
/// Id).
/// </summary>
public sealed class NotificationDedupeKeyConfiguration : IEntityTypeConfiguration<NotificationDedupeKey>
{
    public void Configure(EntityTypeBuilder<NotificationDedupeKey> builder)
    {
        builder.ToTable("notification_dedupe_keys", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.DedupeKey)
            .HasColumnName("dedupe_key")
            .HasMaxLength(200);

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.Property(x => x.PushStatus)
            .HasColumnName("push_status")
            .HasMaxLength(20);

        builder.Property(x => x.SmsStatus)
            .HasColumnName("sms_status")
            .HasMaxLength(20);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        builder.HasIndex(x => x.DedupeKey)
            .HasDatabaseName("uq_notification_dedupe_keys_dedupe_key")
            .IsUnique();
    }
}
