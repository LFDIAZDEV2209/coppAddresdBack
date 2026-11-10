using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoppAddresd.Application.Interfaces;

/// <summary>Contrato interno de narrativa bilingüe; el servicio no devuelve decisiones de contenido.</summary>
public interface IProgramNarrativeClient
{
    Task<ProgramNarrativeResponse> GenerateAsync(ProgramNarrativeRequest request, CancellationToken ct);
}

public sealed record ProgramNarrativeRequest(
    [property: JsonPropertyName("profile_snapshot")] JsonElement ProfileSnapshot,
    [property: JsonPropertyName("proposal")] JsonElement Proposal,
    [property: JsonPropertyName("source_refs")] IReadOnlyList<string> SourceRefs,
    [property: JsonPropertyName("language")] string Language = "es")
{
    [JsonIgnore]
    public Guid? SubjectPatientId { get; init; }
}

/// <summary>Resultado normalizado para persistencia; el contrato HTTP se adapta en Infrastructure.</summary>
public sealed record ProgramNarrativeResponse(
    string NarrativeEs, string NarrativeEn, string? ModelName = null);
