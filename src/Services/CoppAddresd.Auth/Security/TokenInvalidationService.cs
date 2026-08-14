using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Security;

public class TokenInvalidationService : ITokenInvalidationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuthDbContext _dbContext;
    private readonly ILogger<TokenInvalidationService> _logger;

    public TokenInvalidationService(
        UserManager<ApplicationUser> userManager,
        AuthDbContext dbContext,
        ILogger<TokenInvalidationService> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task InvalidateUserTokensAsync(Guid userId, CancellationToken ct = default)
    {
        // UserManager no expone overloads con CancellationToken (limitación de
        // la API Identity): se verifica cancelación antes de cada operación
        // asíncrona para no iniciar trabajo ya cancelado.
        ct.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            _logger.LogWarning("Cannot invalidate tokens: user {UserId} not found", userId);
            return;
        }

        ct.ThrowIfCancellationRequested();
        await _userManager.UpdateSecurityStampAsync(user);
        _logger.LogInformation("Security stamp updated for user {UserId}, all tokens invalidated", userId);
    }

    public async Task InvalidateUsersTokensAsync(IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        var ids = userIds as IReadOnlyCollection<Guid> ?? userIds.ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var newStamp = Guid.NewGuid().ToString();

        // Un solo UPDATE para todos los afectados (REQ-INVALID-05): los usuarios
        // de la mutación de rol NO están tracked por este contexto (se consultan
        // solo sus ids), por lo que ExecuteUpdateAsync no deja entidades stale
        // que un SaveChanges posterior pudiera revertir.
        var affected = await _dbContext.Users
            .Where(u => ids.Contains(u.Id))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(u => u.SecurityStamp, newStamp),
                ct);

        // Fallo parcial sano: los ids inexistentes simplemente no se actualizan;
        // el conteo logueado refleja cuántos usuarios quedaron invalidados.
        _logger.LogInformation(
            "Security stamp updated for {Affected} of {Requested} users in a single batch, all tokens invalidated",
            affected, ids.Count);
    }
}
