using CoppAddresd.Auth.Data;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Services;

/// <summary>
/// Anonimiza de forma irreversible las cuentas cuya retención de 90 días venció.
/// Los datos clínicos del esquema <c>app.*</c> los purga su propio servicio.
/// </summary>
public class AccountPurgeService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccountPurgeService> _logger;

    public AccountPurgeService(IServiceScopeFactory scopeFactory, ILogger<AccountPurgeService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await PurgeExpiredAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Account purge run failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> PurgeExpiredAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var now = DateTime.UtcNow;

        var users = await db.Users
            .Where(u => u.PurgeAfter != null && u.PurgeAfter <= now && u.PurgedAt == null && !u.IsActive)
            .ToListAsync(ct);

        foreach (var user in users)
        {
            var placeholder = $"deleted-{user.Id:N}@deleted.invalid";
            user.Email = placeholder;
            user.NormalizedEmail = placeholder.ToUpperInvariant();
            user.UserName = placeholder;
            user.NormalizedUserName = placeholder.ToUpperInvariant();
            user.FirstName = "Cuenta";
            user.LastName = "eliminada";
            user.PhoneNumber = null;
            user.PasswordHash = null;
            user.SecurityStamp = Guid.NewGuid().ToString();
            user.PurgedAt = now;
            user.UpdatedAt = now;

            // Perfil del paciente (app.patient_profiles): se borran los identificadores
            // personales; los registros clínicos quedan sin vínculo identificable.
            await db.Database.ExecuteSqlRawAsync(
                """
                UPDATE app.patient_profiles SET
                    first_name = 'Cuenta', last_name = 'eliminada', middle_name = NULL,
                    document_number = NULL, date_of_birth = NULL, phone_country_code = NULL,
                    phone_number = NULL, email = NULL, address = NULL, postal_code = NULL,
                    emergency_contact = NULL, member_id = NULL, league_nickname = NULL,
                    league_opt_in = FALSE, notes = NULL, medical_record_number = NULL,
                    updated_at = {0}
                WHERE user_id = {1}
                """,
                [now, user.Id], ct);

            db.RefreshTokens.RemoveRange(db.RefreshTokens.Where(rt => rt.UserId == user.Id));
            db.UserPreferences.RemoveRange(db.UserPreferences.Where(p => p.UserId == user.Id));
        }

        await db.SaveChangesAsync(ct);

        // Códigos de entrega app → web ya usados o vencidos: no tienen valor tras un día.
        await db.AccountDeletionHandoffs
            .Where(h => h.ExpiresAt < now.AddDays(-1))
            .ExecuteDeleteAsync(ct);
        await db.AccountDeletionSessions
            .Where(s => s.ExpiresAt < now.AddDays(-1))
            .ExecuteDeleteAsync(ct);

        if (users.Count > 0)
        {
            _logger.LogInformation("Purged {Count} expired accounts", users.Count);
        }
        return users.Count;
    }
}
