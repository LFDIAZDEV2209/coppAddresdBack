using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Persistence;

/// <summary>
/// DbContext principal. El activity log se mapea al schema <c>audit</c>,
/// los perfiles de la app móvil al schema <c>app</c> y las entidades del
/// ERP al schema <c>erp</c>. El schema <c>public</c> queda reservado.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<PatientProfile> PatientProfiles => Set<PatientProfile>();
    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<MediaItem> MediaItems => Set<MediaItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
