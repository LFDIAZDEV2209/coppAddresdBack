using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

/// <summary>
/// Configuración de la tabla community.event_attendances (una asistencia por
/// evento + perfil).
/// </summary>
public sealed class EventAttendanceConfiguration : IEntityTypeConfiguration<EventAttendance>
{
    public void Configure(EntityTypeBuilder<EventAttendance> builder)
    {
        builder.ToTable("event_attendances", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(x => x.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Event).WithMany(e => e.Attendances).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Profile).WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.EventId, x.ProfileId }).IsUnique().HasDatabaseName("ix_event_attendances_event_id_profile_id");
        builder.HasIndex(x => x.ProfileId).HasDatabaseName("ix_event_attendances_profile_id");
    }
}