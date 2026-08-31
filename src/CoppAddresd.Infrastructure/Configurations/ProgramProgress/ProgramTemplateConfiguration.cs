using System.Text.Json;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>Configuración de plantillas de programa en el schema <c>app</c>.</summary>
public sealed class ProgramTemplateConfiguration : IEntityTypeConfiguration<ProgramTemplate>
{
    public void Configure(EntityTypeBuilder<ProgramTemplate> builder)
    {
        builder.ToTable("program_templates", "app", t =>
        {
            t.HasCheckConstraint("ck_program_templates_total_weeks_positive", "\"total_weeks\" > 0");
            t.HasCheckConstraint("ck_program_templates_streak_min_tasks_positive", "\"streak_min_tasks\" >= 1");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(40);

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(120);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(x => x.TotalWeeks)
            .HasColumnName("total_weeks")
            .HasDefaultValue(83);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(TemplateStatus.Draft);

        builder.Property(x => x.Version)
            .HasColumnName("version")
            .HasDefaultValue(1);

        // Umbral de racha configurable (SPEC §17, A): mínimo de tareas por día
        // para mantener la racha. Default 1 (comportamiento previo).
        builder.Property(x => x.StreakMinTasks)
            .HasColumnName("streak_min_tasks")
            .HasDefaultValue((short)1);

        // Códigos de tarea esenciales para el rescate con congelamiento
        // (SPEC §17, A/C): se persisten como jsonb (lista serializada).
        builder.Property(x => x.EssentialTaskCodes)
            .HasColumnName("essential_task_codes")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .HasDefaultValueSql("'[]'::jsonb");

        // Los actores (created_by/updated_by) apuntan a auth.users; la FK se
        // crea por SQL en la migración (fuera del modelo EF).
        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(x => x.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.PublishedAt)
            .HasColumnName("published_at")
            .HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.Code)
            .HasDatabaseName("ix_program_templates_code")
            .IsUnique();

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_program_templates_status");

        // Relationships
        builder.HasMany(x => x.DayTemplates)
            .WithOne(x => x.Template)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}