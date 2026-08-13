using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración del empleado en el schema <c>erp</c>.
/// El FK hacia auth.users se crea por SQL en la migración (fuera del modelo EF).
/// </summary>
public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees", "erp");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.Property(x => x.JobTitle)
            .HasColumnName("job_title")
            .HasMaxLength(100);

        builder.Property(x => x.Department)
            .HasColumnName("department")
            .HasMaxLength(100);

        builder.Property(x => x.HireDate)
            .HasColumnName("hire_date")
            .HasColumnType("date");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Un empleado = un usuario (relación 1:1 con auth.users).
        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_employees_user_id")
            .IsUnique();
    }
}