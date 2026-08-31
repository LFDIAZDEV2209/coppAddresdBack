using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>
/// Configuración de <c>app.health_score_weights</c> (SPEC §13.1.1): pesos por
/// dimensión del Índice de Salud, únicos por <c>dimension</c>, en
/// <c>numeric(5,4)</c> con CHECK 0..1. La FK de actores a <c>auth.users</c> se
/// crea por SQL en la migración (sin navegación EF).
/// </summary>
public sealed class HealthScoreWeightConfiguration : IEntityTypeConfiguration<HealthScoreWeight>
{
    public void Configure(EntityTypeBuilder<HealthScoreWeight> builder)
    {
        builder.ToTable("health_score_weights", "app", t =>
        {
            t.HasCheckConstraint("ck_health_score_weights_weight_range", "\"weight\" >= 0 AND \"weight\" <= 1");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        // dimension se persiste en minúscula (nombres del enum = valores DB):
        // adherence, clinical, nutrition, psychology, exercise (SPEC §13.1.1).
        builder.Property(x => x.Dimension)
            .HasColumnName("dimension")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(x => x.Weight)
            .HasColumnName("weight")
            .HasPrecision(5, 4);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        // Actores hacia auth.users (FK por SQL en la migración, SET NULL).
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

        // Indexes / constraints (SPEC §13.1.1)
        builder.HasIndex(x => x.Dimension)
            .HasDatabaseName("uq_health_score_weights_dimension")
            .IsUnique();
    }
}