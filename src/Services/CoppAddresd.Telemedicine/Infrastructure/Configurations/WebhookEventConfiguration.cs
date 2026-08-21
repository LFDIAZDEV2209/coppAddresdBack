using CoppAddresd.Telemedicine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Telemedicine.Infrastructure.Configurations;

/// <summary>
/// Configuración EF del registro de webhooks procesados. El índice único
/// <c>(event_type, room_sid, participant_sid)</c> es la garantía de idempotencia
/// en BD: los duplicados concurrentes se serializan y el segundo insert falla
/// (el evento ya fue procesado).
/// </summary>
public sealed class WebhookEventConfiguration : IEntityTypeConfiguration<TelemedicineWebhookEvent>
{
    public void Configure(EntityTypeBuilder<TelemedicineWebhookEvent> builder)
    {
        builder.ToTable("telemedicine_webhook_events");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType).HasMaxLength(64);
        builder.Property(x => x.RoomSid).HasMaxLength(128);
        builder.Property(x => x.ParticipantSid).HasMaxLength(128);

        // ParticipantSid no nulo (vacío para eventos de sala) para que el índice
        // único funcione: NULL permite filas duplicadas en índices únicos.
        builder.Property(x => x.ParticipantSid).IsRequired();

        builder.HasIndex(x => new { x.EventType, x.RoomSid, x.ParticipantSid })
            .IsUnique();
    }
}
