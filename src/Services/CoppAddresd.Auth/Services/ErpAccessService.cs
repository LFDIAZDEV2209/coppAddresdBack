using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Services;

/// <summary>Suspensión ERP atómica y journal durable para reconciliar el directorio sin transacciones HTTP.</summary>
public sealed class ErpAccessService(AuthDbContext db)
{
    public async Task<ErpAccessOperation> ChangeAsync(
        Guid operationId,
        Guid userId,
        Guid employeeId,
        string status,
        CancellationToken ct
    )
    {
        if (status is not ("Active" or "Inactive") || operationId == Guid.Empty)
            throw new ArgumentException("Estado u operación inválidos.");
        var previous = await db
            .ErpAccessOperations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == operationId, ct);
        if (previous is not null)
        {
            if (
                previous.UserId != userId
                || previous.EmployeeId != employeeId
                || previous.Status != status
            )
                throw new InvalidOperationException(
                    "El identificador de operación ya se utilizó con otros datos."
                );
            return previous;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var access =
            await db
                .UserApplications.Include(x => x.Application)
                .SingleOrDefaultAsync(x => x.UserId == userId && x.Application.Code == "erp", ct)
            ?? throw new InvalidOperationException(
                "El profesional no tiene una asignación ERP previa."
            );
        if (!access.Application.IsActive)
            throw new InvalidOperationException("La aplicación ERP no está habilitada.");
        if (
            await db.ErpAccessOperations.AnyAsync(
                x => x.UserId == userId && x.CompletedAt == null,
                ct
            )
        )
            throw new InvalidOperationException(
                "Hay un cambio de acceso en proceso. Espera a que termine."
            );

        access.IsSuspended = status == "Inactive";
        access.SessionVersion = checked(access.SessionVersion + 1);
        var operation = new ErpAccessOperation
        {
            Id = operationId,
            UserId = userId,
            EmployeeId = employeeId,
            Status = status,
            SessionVersion = access.SessionVersion,
        };
        db.ErpAccessOperations.Add(operation);
        await db
            .RefreshTokens.Where(x =>
                x.UserId == userId && x.ApplicationId == access.ApplicationId && x.RevokedAt == null
            )
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return operation;
    }

    public Task<List<ErpAccessOperation>> PendingAsync(CancellationToken ct) =>
        db
            .ErpAccessOperations.AsNoTracking()
            .Where(x => x.CompletedAt == null)
            .OrderBy(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

    public Task<List<ErpAccessOperation>> PendingForAsync(
        Guid[] employeeIds,
        CancellationToken ct
    ) =>
        db
            .ErpAccessOperations.AsNoTracking()
            .Where(x => x.CompletedAt == null && employeeIds.Contains(x.EmployeeId))
            .ToListAsync(ct);

    public async Task CompleteAsync(Guid id, CancellationToken ct) =>
        await db
            .ErpAccessOperations.Where(x => x.Id == id && x.CompletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CompletedAt, DateTime.UtcNow), ct);
}
