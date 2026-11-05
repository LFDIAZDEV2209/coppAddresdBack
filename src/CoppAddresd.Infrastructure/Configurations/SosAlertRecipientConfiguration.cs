using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

public sealed class SosAlertRecipientConfiguration : IEntityTypeConfiguration<SosAlertRecipient>
{
    public void Configure(EntityTypeBuilder<SosAlertRecipient> b)
    {
        b.ToTable("sos_alert_recipients", "app");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.AlertId).HasColumnName("alert_id");
        b.Property(x => x.ContactOrder).HasColumnName("contact_order");
        b.Property(x => x.PhoneE164).HasColumnName("phone_e164").HasMaxLength(20);
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(320);
        b.Property(x => x.SmsChannelStatus).HasColumnName("sms_channel_status").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.SmsDetail).HasColumnName("sms_detail").HasMaxLength(100);
        b.Property(x => x.SmsProviderMessageId).HasColumnName("sms_provider_message_id").HasMaxLength(40);
        b.Property(x => x.SmsDeliveryStatus).HasColumnName("sms_delivery_status").HasMaxLength(30);
        b.Property(x => x.SmsUpdatedAt).HasColumnName("sms_updated_at");
        b.Property(x => x.VoiceChannelStatus).HasColumnName("voice_channel_status").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.VoiceDetail).HasColumnName("voice_detail").HasMaxLength(100);
        b.Property(x => x.VoiceProviderCallId).HasColumnName("voice_provider_call_id").HasMaxLength(40);
        b.Property(x => x.VoiceCallStatus).HasColumnName("voice_call_status").HasMaxLength(30);
        b.Property(x => x.VoiceAnsweredBy).HasColumnName("voice_answered_by").HasMaxLength(30);
        b.Property(x => x.VoiceDurationSeconds).HasColumnName("voice_duration_seconds");
        b.Property(x => x.VoiceUpdatedAt).HasColumnName("voice_updated_at");
        b.Property(x => x.EmailChannelStatus).HasColumnName("email_channel_status").HasMaxLength(20);
        b.Property(x => x.EmailUpdatedAt).HasColumnName("email_updated_at");
        b.HasIndex(x => new { x.AlertId, x.ContactOrder }).IsUnique().HasDatabaseName("uq_sos_recipient_contact");
        b.HasOne(x => x.Alert).WithMany(x => x.Recipients).HasForeignKey(x => x.AlertId).OnDelete(DeleteBehavior.Cascade);
    }
}
