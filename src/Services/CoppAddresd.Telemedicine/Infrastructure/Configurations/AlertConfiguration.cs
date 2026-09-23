using CoppAddresd.Telemedicine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Telemedicine.Infrastructure.Configurations;

/// <summary>Configuración EF de la bandeja de alertas.</summary>
public sealed class AlertConfiguration : IEntityTypeConfiguration<TelemedicineAlert>
{
    public void Configure(EntityTypeBuilder<TelemedicineAlert> builder)
    {
        builder.ToTable("telemedicine_alerts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RecipientType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Severity).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Title).HasMaxLength(300);
        builder.Property(x => x.Body).HasMaxLength(2000);

        // Lectura típica: bandeja de un destinatario, sin leer primero.
        builder.HasIndex(x => new { x.RecipientUserId, x.ReadAt });
        builder.HasIndex(x => new { x.RecipientType, x.RecipientScopeId });
        builder.HasIndex(x => x.RelatedAppointmentId);
        builder.HasIndex(x => x.CreatedAt);
    }
}

/// <summary>Configuración EF de la configuración por organización/clínica.</summary>
public sealed class SettingsConfiguration : IEntityTypeConfiguration<TelemedicineSettings>
{
    public void Configure(EntityTypeBuilder<TelemedicineSettings> builder)
    {
        builder.ToTable("telemedicine_settings");

        builder.HasKey(x => x.Id);

        // F3: default de BD 3 (profesional + paciente + 1 supervisor). La
        // migración AddRoomChatAndCapacity lo aplica y hace backfill de las
        // filas existentes que quedaron en 2.
        builder.Property(x => x.MaxParticipants).HasDefaultValue(3);

        // F5: gracia de reapertura configurable (default de BD 60, compatible
        // con la constante previa). Rango 5–1440 validado en la aplicación.
        builder.Property(x => x.ReopenGraceMinutes).HasDefaultValue(60);

        // Una sola fila de configuración por organización o por clínica.
        builder.HasIndex(x => new { x.OrganizationId, x.ClinicId }).IsUnique();
    }
}
