namespace CoppAddresd.Application.Interfaces;

/// <summary>Canal acotado de trabajo post-commit. No transporta datos clínicos, solo la identidad de recomendación.</summary>
public interface IProgramNarrativeQueue
{
    bool TryEnqueue(Guid recommendationId, bool regenerate = false);
    IAsyncEnumerable<ProgramNarrativeWork> ReadAllAsync(CancellationToken ct);
    /// <summary>Libera la deduplicación al terminar todos los intentos del trabajo.</summary>
    void Complete(Guid recommendationId);
}

/// <summary>La regeneración explícita conserva los idiomas actuales hasta recibir una respuesta válida.</summary>
public sealed record ProgramNarrativeWork(Guid RecommendationId, bool Regenerate);
