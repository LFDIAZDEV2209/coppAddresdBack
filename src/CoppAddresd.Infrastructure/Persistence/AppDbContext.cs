using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Persistence;

/// <summary>
/// DbContext principal. Las tablas de negocio viven en el schema <c>public</c>;
/// el activity log se mapea al schema <c>audit</c>.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
