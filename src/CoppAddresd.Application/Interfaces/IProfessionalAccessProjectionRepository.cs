namespace CoppAddresd.Application.Interfaces;

/// <summary>Proyección monotónica del estado confirmado por Auth.</summary>
public interface IProfessionalAccessProjectionRepository
{
    Task<bool> ApplyAsync(
        Guid employeeId,
        Guid userId,
        string status,
        long version,
        CancellationToken ct
    );
}
