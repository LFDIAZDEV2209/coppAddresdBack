namespace CoppAddresd.Application.Interfaces;

public sealed record ErpAccessOperation(
    Guid Id,
    Guid UserId,
    Guid EmployeeId,
    string Status,
    long SessionVersion,
    DateTime? CompletedAt
);

public interface IErpAccessClient
{
    Task<ErpAccessOperation> ChangeAsync(
        Guid operationId,
        Guid userId,
        Guid employeeId,
        string status,
        CancellationToken ct
    );
    Task<IReadOnlyList<ErpAccessOperation>> PendingAsync(Guid[]? employeeIds, CancellationToken ct);
    Task CompleteAsync(Guid operationId, CancellationToken ct);
}
