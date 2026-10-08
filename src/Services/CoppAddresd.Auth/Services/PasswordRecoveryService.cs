using System.Security.Cryptography;
using System.Text;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Services;

/// <summary>Recuperación para cuentas existentes. OTP separado del login, de un solo uso y sin aprovisionar usuarios.</summary>
public sealed class PasswordRecoveryService(IPatientLookupService patients, UserManager<ApplicationUser> users,
    AuthDbContext db, ITwilioOtpService sms, IEmailSender email, IOtpProtectionService protection, IHttpContextAccessor context)
{
    private string Ip => context.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    private static string Channel(string contact) => contact == "phone" ? "RecoveryPhone" : "RecoveryEmail";
    private static string Hash(string salt, string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(salt + code)));
    private async Task<(PatientLookupResult Patient, ApplicationUser User)> FindAccount(string document, CancellationToken ct)
    {
        var patient = await patients.FindByDocumentNumberAsync(document, ct);
        var user = patient?.UserId is {} id ? await users.FindByIdAsync(id.ToString()) : null;
        if (patient is null || user is null || !user.IsActive || !await db.UserApplications.AnyAsync(a => a.UserId == user.Id && a.Application.Code == "app" && a.Application.IsActive && !a.IsSuspended, ct))
            throw new InvalidOperationException("No se puede recuperar esta cuenta. Contacta a tu equipo de atención.");
        return (patient, user);
    }
    private static string Target(PatientLookupResult patient, string contact)
    {
        if (contact == "email")
            return !string.IsNullOrWhiteSpace(patient.Email) ? patient.Email.Trim() : throw new InvalidOperationException("Correo no disponible.");
        if (string.IsNullOrWhiteSpace(patient.PhoneNumber)) throw new InvalidOperationException("Teléfono no disponible.");
        var digits = new string(patient.PhoneNumber.Where(char.IsDigit).ToArray());
        // Se respeta el número internacional o el país registrado; nunca se adivina el prefijo.
        var prefix = new string((patient.PhoneCountryCode ?? "").Where(char.IsDigit).ToArray());
        if (patient.PhoneNumber.TrimStart().StartsWith('+')) return "+" + digits;
        if (prefix.Length == 0) throw new InvalidOperationException("Código de país no disponible.");
        return "+" + prefix + digits;
    }

    public async Task<SendOtpResponse> SendAsync(PasswordRecoveryCodeRequest request, CancellationToken ct)
    {
        var (patient, _) = await FindAccount(request.DocumentNumber, ct);
        var target = Target(patient, request.ContactId);
        var guard = protection.CheckCanSend(Ip, patient.DocumentNumber, target);
        if (!guard.Allowed) throw new CoppAddresd.Auth.Exceptions.OtpProtectionException(guard.Reason!.Value, "Intenta más tarde.", guard.RetryAfterSeconds);
        var code = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        if (request.ContactId == "phone") await sms.SendAsync(target, ct);
        else await email.SendAsync(new EmailMessage(target, "Recupera tu contraseña de CoppAddresd",
            $"<p>Tu código para cambiar tu contraseña es <strong>{code}</strong>. Vence en 5 minutos.</p><p>Si no lo solicitaste, ignora este correo.</p>",
            $"Código para cambiar tu contraseña: {code}. Vence en 5 minutos. Si no lo solicitaste, ignora este correo."), ct);
        protection.RegisterSend(Ip, patient.DocumentNumber, target);
        var now = DateTime.UtcNow;
        await db.OtpCodes.Where(o => o.DocumentNumber == patient.DocumentNumber && o.Channel.StartsWith("Recovery") && o.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.UsedAt, now), ct);
        db.OtpCodes.Add(new OtpCode { Id = Guid.NewGuid(), DocumentNumber = patient.DocumentNumber, Channel = Channel(request.ContactId),
            Target = target, Salt = salt, CodeHash = Hash(salt, code), CreatedAt = now, ExpiresAt = now.AddMinutes(5) });
        await db.SaveChangesAsync(ct);
        return new SendOtpResponse(300); // Nunca devuelve códigos de recuperación.
    }

    public async Task<bool> ResetAsync(PasswordRecoveryRequest request, CancellationToken ct)
    {
        var (patient, user) = await FindAccount(request.DocumentNumber, ct);
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, user, request.NewPassword);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
        var channel = Channel(request.ContactId);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // Bloqueo de fila: dos envíos con el mismo código no pueden restablecer dos veces.
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var otp = await db.OtpCodes.FromSqlInterpolated($"SELECT * FROM auth.\"OtpCodes\" WHERE \"DocumentNumber\" = {patient.DocumentNumber} AND \"Channel\" = {channel} AND \"UsedAt\" IS NULL ORDER BY \"CreatedAt\" DESC LIMIT 1 FOR UPDATE").FirstOrDefaultAsync(ct);
            if (otp is null || otp.IsExpired || otp.Attempts >= 5 || otp.Target != Target(patient, request.ContactId)) return false;
            var guard = protection.CheckCanVerify(Ip, otp.Target);
            if (!guard.Allowed) throw new CoppAddresd.Auth.Exceptions.OtpProtectionException(guard.Reason!.Value, "Intenta más tarde.", guard.RetryAfterSeconds);
            var valid = request.ContactId == "phone" ? (await sms.CheckAsync(otp.Target, request.Otp, ct)).IsApproved
                : CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Hash(otp.Salt, request.Otp)), Encoding.UTF8.GetBytes(otp.CodeHash));
            if (!valid)
            {
                otp.Attempts++; protection.RegisterVerifyFailed(Ip, otp.Target);
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return false;
            }
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var changed = await users.ResetPasswordAsync(user, token, request.NewPassword);
            if (!changed.Succeeded) throw new InvalidOperationException("No se pudo cambiar la contraseña.");
            otp.UsedAt = DateTime.UtcNow;
            await db.RefreshTokens.Where(r => r.UserId == user.Id && r.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(r => r.RevokedAt, DateTime.UtcNow), ct);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            protection.RegisterVerifySucceeded(Ip, otp.Target);
            return true;
        });
    }
}
