using CoppAddresd.Auth.Authorization;
using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Auth.Controllers;

/// <summary>
/// Invitaciones de primer acceso (onboarding del profesional). El ERP crea el
/// usuario sin password vía el endpoint interno; el profesional acepta con un
/// enlace de un solo uso y establece su propia contraseña (nunca se envían
/// credenciales por correo).
/// </summary>
[ApiController]
public class InvitationsController(
    IInvitationService invitations,
    IEmailSender emailSender,
    UserManager<ApplicationUser> userManager,
    AuthDbContext dbContext,
    IOptions<EmailSettings> emailSettings,
    ILogger<InvitationsController> logger) : ControllerBase
{
    private readonly EmailSettings _emailSettings = emailSettings.Value;

    /// <summary>
    /// Crea el usuario (sin password) + acceso ERP + invitación y envía el
    /// correo. Lo invoca el ERP con <c>X-Internal-Key</c>. El token se
    /// devuelve SOLO con el provider "Log" (dev); en producción la única vía
    /// es el correo.
    /// </summary>
    [HttpPost("api/auth/internal/invitations")]
    [AllowAnonymous]
    [RequireInternalKey]
    public async Task<ActionResult<object>> CreateInvitation(
        [FromBody] CreateInvitationInternalRequest request,
        CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Conflict(new { message = $"Ya existe un usuario con el correo '{email}'." });
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            IsActive = true,
            EmailConfirmed = false,
        };

        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            return BadRequest(new { message = errors });
        }

        // Acceso a la aplicación ERP (puede iniciar sesión tras aceptar).
        var erpApplication = await dbContext.Applications
            .FirstOrDefaultAsync(a => a.Code == ApplicationCodes.Erp, ct);
        if (erpApplication is not null)
        {
            dbContext.UserApplications.Add(new UserApplication
            {
                UserId = user.Id,
                ApplicationId = erpApplication.Id,
                CreatedAt = DateTime.UtcNow,
            });
            await dbContext.SaveChangesAsync(ct);
        }

        var (success, error, invitation, token) = await invitations.CreateAsync(user.Id, request.CreatedBy, ct);
        if (!success || invitation is null || token is null)
        {
            return BadRequest(new { message = error ?? "No se pudo crear la invitación." });
        }

        var link = $"{_emailSettings.FrontendUrl}/invitaciones?token={token}";
        await SendInvitationEmailAsync(user, link, ct);

        return Ok(new
        {
            userId = user.Id,
            invitationId = invitation.Id,
            expiresAt = invitation.ExpiresAt,
            // Solo en dev (provider Log) se expone el enlace; en producción el
            // correo es la única vía.
            link = IsLogProvider() ? link : null,
        });
    }

    /// <summary>Valida el token (página pública de aceptación). No lo consume.</summary>
    [HttpGet("api/invitations/validate")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> Validate([FromQuery] string token, CancellationToken ct)
    {
        var (valid, error, invitation) = await invitations.ValidateAsync(token, ct);
        if (!valid || invitation is null)
        {
            return Ok(new { valid = false, error });
        }

        var user = await userManager.FindByIdAsync(invitation.UserId.ToString());
        return Ok(new
        {
            valid = true,
            email = user?.Email,
            firstName = user?.FirstName,
            lastName = user?.LastName,
            expiresAt = invitation.ExpiresAt,
        });
    }

    /// <summary>Acepta la invitación: establece el password y la marca usada.</summary>
    [HttpPost("api/invitations/accept")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> Accept(
        [FromBody] AcceptInvitationRequest request,
        CancellationToken ct)
    {
        var (success, error) = await invitations.AcceptAsync(request.Token, request.Password, ct);
        if (!success)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new { success = true });
    }

    /// <summary>Revoca la invitación pendiente. Lo invoca el ERP con X-Internal-Key
    /// como compensación del flujo de creación de profesionales si falla la
    /// aplicación de scopes.</summary>
    [HttpPost("api/auth/internal/invitations/{id:guid}/revoke")]
    [AllowAnonymous]
    [RequireInternalKey]
    public async Task<IActionResult> RevokeInternal(Guid id, CancellationToken ct)
    {
        var (success, error) = await invitations.RevokeAsync(id, ct);
        if (!success)
            return BadRequest(new { message = error });

        return NoContent();
    }

    [HttpPost("api/invitations/{id:guid}/resend")]
    [Authorize]
    [RequireErpAudience]
    [RequirePermission(PermissionCodes.UsersUpdate)]
    public async Task<ActionResult<object>> Resend(Guid id, CancellationToken ct)
    {
        var (success, error, invitation, token) = await invitations.ResendAsync(id, GetCallerId(), ct);
        if (!success || invitation is null || token is null)
        {
            return BadRequest(new { message = error ?? "No se pudo reenviar la invitación." });
        }

        var user = await userManager.FindByIdAsync(invitation.UserId.ToString());
        var link = $"{_emailSettings.FrontendUrl}/invitaciones?token={token}";
        if (user is not null)
        {
            await SendInvitationEmailAsync(user, link, ct);
        }

        return Ok(new
        {
            invitationId = invitation.Id,
            expiresAt = invitation.ExpiresAt,
            link = IsLogProvider() ? link : null,
        });
    }

    [HttpPost("api/invitations/{id:guid}/revoke")]
    [Authorize]
    [RequireErpAudience]
    [RequirePermission(PermissionCodes.UsersUpdate)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var (success, error) = await invitations.RevokeAsync(id, ct);
        if (!success)
        {
            return BadRequest(new { message = error });
        }

        return NoContent();
    }

    private async Task SendInvitationEmailAsync(ApplicationUser user, string link, CancellationToken ct)
    {
        var message = new EmailMessage(
            To: user.Email!,
            Subject: "Te invitamos a CoppAddresd — completa tu acceso",
            HtmlBody: $"""
                <h2>¡Hola {user.FirstName} {user.LastName}!</h2>
                <p>Has sido invitado/a a la plataforma CoppAddresd.</p>
                <p>Para completar tu primer acceso y establecer tu contraseña, abre este enlace
                (válido por 72 horas):</p>
                <p><a href="{link}">{link}</a></p>
                <p>Si no esperabas esta invitación, ignora este correo.</p>
                """);

        try
        {
            await emailSender.SendAsync(message, ct);
        }
        catch (Exception ex)
        {
            // El correo no bloquea la creación: se loguea y el ERP puede reenviar.
            logger.LogError(ex, "Fallo al enviar invitación a {To}", message.To);
        }
    }

    private bool IsLogProvider()
        => _emailSettings.Provider.Equals("Log", StringComparison.OrdinalIgnoreCase);

    private Guid? GetCallerId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

public record CreateInvitationInternalRequest(
    string Email,
    string FirstName,
    string LastName,
    Guid? CreatedBy = null);

public record AcceptInvitationRequest(string Token, string Password);
