using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración del token de dispositivo en el schema <c>app</c>.
/// El FK hacia auth.users se crea por SQL en la migración (fuera del modelo
/// EF), igual que en patient_profiles: la tabla Users vive en el schema auth
/// y la gestiona el Auth Service.
/// </summary>
public sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("device_tokens", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.Property(x => x.Token)
            .HasColumnName("token")
            .HasMaxLength(255);

        builder.Property(x => x.Platform)
            .HasColumnName("platform")
            .HasMaxLength(10);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Un usuario no puede registrar dos veces el mismo token (mismo
        // dispositivo). El upsert del handler depende de este índice.
        builder.HasIndex(x => new { x.UserId, x.Token })
            .HasDatabaseName("ix_device_tokens_user_id_token")
            .IsUnique();
    }
}