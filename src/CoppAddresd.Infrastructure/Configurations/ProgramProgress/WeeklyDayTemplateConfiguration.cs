using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>Configuración del catálogo por día de la plantilla en el schema <c>app</c>.</summary>
public sealed class WeeklyDayTemplateConfiguration : IEntityTypeConfiguration<WeeklyDayTemplate>
{
    public void Configure(EntityTypeBuilder<WeeklyDayTemplate> builder)
    {
        builder.ToTable(
            "weekly_day_templates",
            "app",
            t =>
            {
                t.HasCheckConstraint(
                    "ck_weekly_day_templates_weekday_range",
                    "\"weekday\" BETWEEN 1 AND 7"
                );
                t.HasCheckConstraint(
                    "ck_weekly_day_templates_points_non_negative",
                    "\"points\" >= 0"
                );
            }
        );

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.TemplateId).HasColumnName("template_id");

        builder.Property(x => x.Weekday).HasColumnName("weekday");

        builder
            .Property(x => x.TaskCode)
            .HasColumnName("task_code")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(x => x.Points).HasColumnName("points");

        builder.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);

        // Fallback P1 de contenido (podcast) a nivel plantilla (SPEC §4.4);
        // FK → app.media_items con Restrict para preservar la fila si el medio se elimina.
        builder.Property(x => x.MediaId).HasColumnName("media_id");

        // T-77 (per-day routine): vínculos de contenido por día — rutina de
        // ejercicio y plan nutricional. Columnas creadas por la migración
        // AddWeeklyDayTemplateContentLinks; las FKs se definen por SQL en la
        // migración (misma convención que weaknesses/assigned_to).
        builder.Property(x => x.RoutineId).HasColumnName("routine_id");

        builder.Property(x => x.NutritionPlanId).HasColumnName("nutrition_plan_id");

        // El actor (created_by) apunta a auth.users; la FK se crea por SQL en la
        // migración (fuera del modelo EF).
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");

        builder
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(x => x.TemplateId).HasDatabaseName("ix_weekly_day_templates_template_id");

        builder
            .HasIndex(x => new
            {
                x.TemplateId,
                x.Weekday,
                x.TaskCode,
            })
            .HasDatabaseName("uq_weekly_day_templates_template_weekday_task")
            .IsUnique();

        builder.HasIndex(x => x.MediaId).HasDatabaseName("ix_weekly_day_templates_media_id");

        // Relationships
        builder
            .HasOne(x => x.Media)
            .WithMany()
            .HasForeignKey(x => x.MediaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
