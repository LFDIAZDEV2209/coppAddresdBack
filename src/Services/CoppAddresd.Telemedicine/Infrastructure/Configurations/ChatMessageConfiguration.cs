using CoppAddresd.Telemedicine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Telemedicine.Infrastructure.Configurations;

/// <summary>
/// Configuración EF del chat clínico (F3). El índice
/// <c>(appointment_id, created_at)</c> cubre la lectura incremental de la
/// consulta por cursor; el rol del emisor se persiste como string acotado.
/// </summary>
public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("chat_messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SenderRole).HasMaxLength(32);
        builder.Property(x => x.Body).HasMaxLength(2000);

        builder.HasIndex(x => new { x.AppointmentId, x.CreatedAt });
    }
}
