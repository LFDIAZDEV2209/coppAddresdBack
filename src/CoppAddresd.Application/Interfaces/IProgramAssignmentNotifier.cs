using CoppAddresd.Application.Features.ProgramProgress.Assignment;

namespace CoppAddresd.Application.Interfaces;

/// <summary>Entrega post-commit de avisos persistidos; fallar el proveedor no revierte una decisión clínica.</summary>
public interface IProgramAssignmentNotifier
{
    Task<int> DispatchAsync(Guid? recommendationId, DateTime utcNow, CancellationToken ct);
}
