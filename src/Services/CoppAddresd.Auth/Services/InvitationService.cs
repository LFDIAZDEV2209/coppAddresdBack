using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace CoppAddresd.Auth.Services;

/// <summary>
/// Implementación del ciclo de vida de invitaciones. El token se genera con
/// 32 bytes aleatorios (codificación base64url) y se guarda su SHA-256; la
/// aceptación usa el password policy de Identity (AddPasswordAsync).
/// </summary>
public class InvitationService(
    AuthDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ILogger<InvitationService> logger) : IInvitationService
{
    private static readonly TimeSpan DefaultValidity = TimeSpan.FromHours(72);

    public async Task<(bool Success, string? Error, Invitation? Invitation, string? Token)> CreateAsync(
        Guid userId,
        Guid? createdBy,
        CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return (false, "Usuario no encontrado", null, null);
        }

        // Una sola pendiente a la vez: revoca las anteriores.
        await RevokePendingAsync(userId, ct);

        var token = GenerateToken();
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(token),
            ExpiresAt = DateTime.UtcNow.Add(DefaultValidity),
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.Invitations.Add(invitation);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Invitación creada para el usuario {UserId} (expira {ExpiresAt})",
            userId, invitation.ExpiresAt);

        return (true, null, invitation, token);
    }

    public async Task<(bool Valid, string? Error, Invitation? Invitation)> ValidateAsync(
        string token,
        CancellationToken ct = default)
    {
        var invitation = await FindByTokenAsync(token, ct);
        if (invitation is null)
        {
            return (false, "Enlace de invitación inválido.", null);
        }

        if (invitation.UsedAt is not null)
        {
            return (false, "Esta invitación ya fue utilizada.", invitation);
        }

        if (invitation.RevokedAt is not null)
        {
            return (false, "Esta invitación fue revocada. Solicita un nuevo enlace.", invitation);
        }

        if (invitation.ExpiresAt <= DateTime.UtcNow)
        {
            return (false, "Esta invitación expiró. Solicita un nuevo enlace.", invitation);
        }

        return (true, null, invitation);
    }

    public async Task<(bool Success, string? Error)> AcceptAsync(
        string token,
        string password,
        CancellationToken ct = default)
    {
        var (valid, error, invitation) = await ValidateAsync(token, ct);
        if (!valid || invitation is null)
        {
            return (false, error);
        }

        var user = await userManager.FindByIdAsync(invitation.UserId.ToString());
        if (user is null)
        {
            return (false, "Usuario no encontrado.");
        }

        if (!user.IsActive)
        {
            return (false, "Tu cuenta está desactivada. Contacta al administrador.");
        }

        // Carga TRACKED para persistir UsedAt (FindByTokenAsync usa AsNoTracking).
        var tracked = await dbContext.Invitations
            .FirstAsync(i => i.Id == invitation.Id, ct);

        if (!await userManager.HasPasswordAsync(user))
        {
            var result = await userManager.AddPasswordAsync(user, password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, errors);
            }

            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        tracked.UsedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Invitación aceptada por el usuario {UserId}", user.Id);
        return (true, null);
    }

    public async Task<(bool Success, string? Error, Invitation? Invitation, string? Token)> ResendAsync(
        Guid invitationId,
        Guid? createdBy,
        CancellationToken ct = default)
    {
        var invitation = await dbContext.Invitations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invitationId, ct);

        if (invitation is null)
        {
            return (false, "Invitación no encontrada", null, null);
        }

        if (invitation.UsedAt is not null)
        {
            return (false, "La invitación ya fue utilizada; no se puede reenviar.", null, null);
        }

        // Revoca la actual y crea una nueva para el mismo usuario.
        invitation.RevokedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return await CreateAsync(invitation.UserId, createdBy, ct);
    }

    public async Task<(bool Success, string? Error)> RevokeAsync(
        Guid invitationId,
        CancellationToken ct = default)
    {
        var invitation = await dbContext.Invitations
            .FirstOrDefaultAsync(i => i.Id == invitationId, ct);

        if (invitation is null)
        {
            return (false, "Invitación no encontrada");
        }

        if (invitation.UsedAt is not null)
        {
            return (false, "La invitación ya fue utilizada; no se puede revocar.");
        }

        invitation.RevokedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Invitación {InvitationId} revocada", invitationId);
        return (true, null);
    }

    private async Task RevokePendingAsync(Guid userId, CancellationToken ct)
    {
        await dbContext.Invitations
            .Where(i => i.UserId == userId && i.UsedAt == null && i.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.RevokedAt, DateTime.UtcNow), ct);
    }

    private async Task<Invitation?> FindByTokenAsync(string token, CancellationToken ct)
        => await dbContext.Invitations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.TokenHash == Hash(token), ct);

    private static string GenerateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
