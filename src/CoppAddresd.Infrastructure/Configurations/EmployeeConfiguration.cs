using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>Configuración del empleado (núcleo HR) en el schema <c>erp</c>.</summary>
public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees", "erp");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.ErpAccessVersion)
            .HasColumnName("erp_access_version").HasDefaultValue(0L);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.Property(x => x.OrganizationId)
            .HasColumnName("organization_id")
            .IsRequired();

        builder.Property(x => x.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.MiddleName)
            .HasColumnName("middle_name")
            .HasMaxLength(100);

        builder.Property(x => x.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(x => x.PhoneCountryCode)
            .HasColumnName("phone_country_code")
            .HasMaxLength(10);

        builder.Property(x => x.PhoneNumber)
            .HasColumnName("phone_number")
            .HasMaxLength(20);

        builder.Property(x => x.JobTitle)
            .HasColumnName("job_title")
            .HasMaxLength(100);

        builder.Property(x => x.Department)
            .HasColumnName("department")
            .HasMaxLength(100);

        builder.Property(x => x.HireDate)
            .HasColumnName("hire_date")
            .HasColumnType("date");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        builder.HasOne(x => x.Organization)
            .WithMany(o => o.Employees)
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        // 1:0..1 con la extensión clínica (cascade: borrar el empleado borra su perfil clínico).
        builder.HasOne(x => x.Professional)
            .WithOne(p => p.Employee)
            .HasForeignKey<Professional>(p => p.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // El FK hacia auth.users se crea por SQL en la migración (fuera del modelo EF),
        // igual que el resto del schema (el Auth Service es dueño de esa tabla).

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_employees_user_id")
            .IsUnique();

        builder.HasIndex(x => x.OrganizationId)
            .HasDatabaseName("ix_employees_organization_id");

        // Email único por organización (identidad de la invitación).
        builder.HasIndex(x => new { x.OrganizationId, x.Email })
            .HasDatabaseName("ix_employees_organization_email")
            .IsUnique();

        // Filtro frecuente del directorio.
        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_employees_status");
    }
}
