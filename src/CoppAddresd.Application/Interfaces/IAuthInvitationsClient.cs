namespace CoppAddresd.Application.Interfaces;

/// <summary>Resultado de crear una invitación en el Auth Service.</summary>
public record InvitationCreationResult(
    Guid UserId,
    Guid InvitationId,
    DateTime ExpiresAt,
    string? Link);

/// <summary>
/// Cliente hacia los endpoints internos del Auth Service (X-Internal-Key)
/// para el ciclo de invitaciones del onboarding. La implementación HTTP vive
/// en Infrastructure.
/// </summary>
public interface IAuthInvitationsClient
{
    /// <summary>Crea usuario (sin password) + acceso ERP + invitación y envía el correo.</summary>
    Task<InvitationCreationResult> CreateInvitationAsync(
        string email,
        string firstName,
        string lastName,
        CancellationToken ct = default);
}
