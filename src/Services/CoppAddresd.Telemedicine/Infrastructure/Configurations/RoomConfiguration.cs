using CoppAddresd.Telemedicine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Telemedicine.Infrastructure.Configurations;

/// <summary>Configuración EF de la sala virtual y su historial de sesiones.</summary>
public sealed class RoomConfiguration : IEntityTypeConfiguration<VirtualRoom>
{
    public void Configure(EntityTypeBuilder<VirtualRoom> builder)
    {
        builder.ToTable("virtual_rooms");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider).HasMaxLength(32);
        builder.Property(x => x.ProviderRoomSid).HasMaxLength(128);
        builder.Property(x => x.ProviderRoomName).HasMaxLength(128);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        // Una cita tiene a lo sumo una sala.
        builder.HasIndex(x => x.AppointmentId).IsUnique();

        // Nombre determinista de sala único por proveedor: base de la idempotencia
        // (reintentar la creación devuelve la misma sala).
        builder.HasIndex(x => new { x.Provider, x.ProviderRoomName }).IsUnique();

        builder.HasIndex(x => x.ProviderRoomSid);

        builder.HasMany(x => x.Sessions)
            .WithOne(s => s.Room)
            .HasForeignKey(s => s.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Configuración EF de la sesión de video.</summary>
public sealed class SessionConfiguration : IEntityTypeConfiguration<TelemedicineSession>
{
    public void Configure(EntityTypeBuilder<TelemedicineSession> builder)
    {
        builder.ToTable("telemedicine_sessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.EndReason).HasMaxLength(500);

        builder.HasIndex(x => x.AppointmentId);
        builder.HasIndex(x => x.RoomId);
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.Encounter)
            .WithOne(e => e.Session)
            .HasForeignKey<ClinicalEncounter>(e => e.SessionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
