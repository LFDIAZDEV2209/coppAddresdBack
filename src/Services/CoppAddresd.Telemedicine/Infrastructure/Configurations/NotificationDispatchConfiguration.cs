using CoppAddresd.Telemedicine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Telemedicine.Infrastructure.Configurations;

/// <summary>
/// Configuración EF del registro de despachos de notificación (F2). El índice
/// único <c>(appointment_id, kind)</c> es la garantía de "una sola vez" por
/// recordatorio y cita, incluso entre réplicas del microservicio.
/// </summary>
public sealed class NotificationDispatchConfiguration
    : IEntityTypeConfiguration<NotificationDispatch>
{
    public void Configure(EntityTypeBuilder<NotificationDispatch> builder)
    {
        builder.ToTable("notification_dispatch");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(x => new { x.AppointmentId, x.Kind }).IsUnique();
    }
}
