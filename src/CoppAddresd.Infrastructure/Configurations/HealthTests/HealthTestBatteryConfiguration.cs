using CoppAddresd.Domain.Entities.HealthTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración de baterías de tests en el schema <c>app</c>.</summary>
public sealed class HealthTestBatteryConfiguration : IEntityTypeConfiguration<HealthTestBattery>
{
    public void Configure(EntityTypeBuilder<HealthTestBattery> builder)
    {
        builder.ToTable("health_test_batteries", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(64);

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);

        builder.Property(x => x.Description).HasColumnName("description");

        builder
            .Property(x => x.AutoAssignOnPatientCreate)
            .HasColumnName("auto_assign_on_patient_create")
            .HasDefaultValue(false);

        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ix_health_test_batteries_code");

        builder.HasIndex(x => x.IsActive).HasDatabaseName("ix_health_test_batteries_is_active");
    }
}
