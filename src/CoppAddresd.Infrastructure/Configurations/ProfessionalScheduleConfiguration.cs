using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de la tabla de horarios semanales de atención en el schema <c>erp</c>.</summary>
public sealed class ProfessionalScheduleConfiguration : IEntityTypeConfiguration<ProfessionalSchedule>
{
    public void Configure(EntityTypeBuilder<ProfessionalSchedule> builder)
    {
        builder.ToTable("professional_schedules", "erp");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ProfessionalId)
            .HasColumnName("professional_id")
            .IsRequired();

        builder.Property(x => x.Weekday)
            .HasColumnName("weekday")
            .IsRequired();

        builder.Property(x => x.StartTime)
            .HasColumnName("start_time")
            .HasColumnType("time");

        builder.Property(x => x.EndTime)
            .HasColumnName("end_time")
            .HasColumnType("time");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // FK → erp.professionals (cascade delete: al borrar el profesional se borran sus horarios).
        builder.HasOne(x => x.Professional)
            .WithMany(p => p.WeeklySchedule)
            .HasForeignKey(x => x.ProfessionalId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unicidad: un profesional solo puede tener una fila por día de la semana.
        builder.HasIndex(x => new { x.ProfessionalId, x.Weekday })
            .HasDatabaseName("uq_professional_schedules_professional_weekday")
            .IsUnique();

        builder.HasIndex(x => x.ProfessionalId)
            .HasDatabaseName("ix_professional_schedules_professional_id");
    }
}
