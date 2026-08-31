using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

/// <summary>
/// Actualización parcial de preferencias del usuario: cada campo presente se
/// valida y actualiza; los ausentes se conservan. Al menos uno debe enviarse.
/// </summary>
public record UpdateUserPreferenceRequest
{
    [MaxLength(2)]
    public string? Lang { get; init; }

    [MaxLength(16)]
    public string? AccentColor { get; init; }
}
