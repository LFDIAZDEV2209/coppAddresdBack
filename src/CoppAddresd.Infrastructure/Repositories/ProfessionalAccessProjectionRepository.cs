using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class ProfessionalAccessProjectionRepository(AppDbContext db)
    : IProfessionalAccessProjectionRepository
{
    public async Task<bool> ApplyAsync(
        Guid employeeId,
        Guid userId,
        string status,
        long version,
        CancellationToken ct
    )
    {
        var employee = db.Employees.Where(x => x.Id == employeeId && x.UserId == userId);
        var updated = await employee
            .Where(x => x.ErpAccessVersion < version)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, status)
                        .SetProperty(x => x.ErpAccessVersion, version)
                        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow),
                ct
            );
        return updated == 1 || await employee.AnyAsync(x => x.ErpAccessVersion >= version, ct);
    }
}
