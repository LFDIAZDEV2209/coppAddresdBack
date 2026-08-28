using CoppAddresd.Domain.Entities.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>
/// Configuración de plantillas de hábito de alimentación/hidratación en el
/// schema <c>app</c> (SPEC §18, A): catálogo sembrado por <c>code</c> (códigos
/// de comida del móvil <c>des</c>/<c>alm</c>/<c>mer</c>/<c>cen</c>/<c>agua</c>)
/// con su categoría (<c>alimentacion</c>/<c>agua</c>).
/// </summary>
public sealed class HabitTemplateConfiguration : IEntityTypeConfiguration<HabitTemplate>
{
    public void Configure(EntityTypeBuilder<HabitTemplate> builder)
    {
        builder.ToTable("habit_templates", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(20);

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(120);

        builder.Property(x => x.Category)
            .HasColumnName("category")
            .HasMaxLength(40);

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(x => x.Code)
            .HasDatabaseName("uq_habit_templates_code")
            .IsUnique();

        builder.HasIndex(x => x.Category)
            .HasDatabaseName("ix_habit_templates_category");
    }
}