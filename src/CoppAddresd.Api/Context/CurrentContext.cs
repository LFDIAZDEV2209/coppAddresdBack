using CoppAddresd.Api.Constants;
using CoppAddresd.Api.Security;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CoppAddresd.Api.Context;

/// <summary>
/// Contexto actual de la petición: usuario autenticado (claims JWT) y
/// contexto organizacional activo (headers <c>X-Clinic-Id</c> /
/// <c>X-Organization-Id</c>). Combina la autorización global (claims) con la
/// scoped (introspección al Auth Service) para decidir permisos efectivos.
/// </summary>
public interface ICurrentContext
{
    Guid? UserId { get; }

    string? SecurityStamp { get; }

    Guid? ActiveClinicId { get; }

    Guid? ActiveOrganizationId { get; }

    /// <summary>Cadena de scopes del contexto activo (clínica → org → Global).</summary>
    Task<IReadOnlyList<ScopeEntry>> BuildScopeChainAsync(CancellationToken ct = default);

    /// <summary>¿Tiene el permiso globalmente (claims) o en el contexto activo (scoped)?</summary>
    Task<bool> HasPermissionAsync(string permissionCode, CancellationToken ct = default);

    /// <summary>
    /// Id del profesional clínico asociado al usuario autenticado
    /// (<c>erp.professionals</c>), o null si el usuario no es profesional.
    /// Se deriva SIEMPRE de la identidad del JWT, nunca de parámetros del
    /// cliente: es la base del alcance de datos "propios".
    /// </summary>
    Task<Guid?> GetProfessionalIdAsync(CancellationToken ct = default);
}

public class CurrentContext(
    IHttpContextAccessor httpContextAccessor,
    IScopedAuthorizationClient scopedClient,
    AppDbContext dbContext) : ICurrentContext
{
    private const string SecurityStampClaim = "security_stamp";

    // Memo por petición: la resolución del profesional consulta la BD una sola
    // vez (puede pedirse en varios puntos del request).
    private Guid? _professionalId;
    private bool _professionalIdResolved;

    public Guid? UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User
                .FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? SecurityStamp
        => httpContextAccessor.HttpContext?.User.FindFirstValue(SecurityStampClaim);

    public Guid? ActiveClinicId => GetHeaderGuid("X-Clinic-Id");

    public Guid? ActiveOrganizationId => GetHeaderGuid("X-Organization-Id");

    public async Task<IReadOnlyList<ScopeEntry>> BuildScopeChainAsync(CancellationToken ct = default)
    {
        if (ActiveClinicId is { } clinicId)
        {
            var organizationId = await dbContext.Clinics
                .AsNoTracking()
                .Where(c => c.Id == clinicId)
                .Select(c => (Guid?)c.OrganizationId)
                .FirstOrDefaultAsync(ct);

            return organizationId is null
                ? [new ScopeEntry("Clinic", clinicId), ScopeEntry.Global]
                : [new ScopeEntry("Clinic", clinicId), new ScopeEntry("Organization", organizationId), ScopeEntry.Global];
        }

        if (ActiveOrganizationId is { } organizationId2)
        {
            return [new ScopeEntry("Organization", organizationId2), ScopeEntry.Global];
        }

        return [ScopeEntry.Global];
    }

    public async Task<bool> HasPermissionAsync(string permissionCode, CancellationToken ct = default)
    {
        if (UserId is not { } userId)
        {
            return false;
        }

        // Permiso global (claims JWT): aplica en cualquier contexto.
        if (httpContextAccessor.HttpContext?.User.HasClaim(PermissionClaimTypes.Permission, permissionCode) == true)
        {
            return true;
        }

        // Permiso scoped: introspección con el contexto activo.
        var chain = await BuildScopeChainAsync(ct);
        if (chain.Count <= 1)
        {
            return false;
        }

        return await scopedClient.AuthorizeAsync(
            userId, SecurityStamp ?? string.Empty, permissionCode, chain, ct);
    }

    private Guid? GetHeaderGuid(string header)
    {
        var value = httpContextAccessor.HttpContext?.Request.Headers[header].ToString();
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public async Task<Guid?> GetProfessionalIdAsync(CancellationToken ct = default)
    {
        if (_professionalIdResolved)
        {
            return _professionalId;
        }

        _professionalId = UserId is { } userId
            ? await dbContext.Employees
                .AsNoTracking()
                .Where(e => e.UserId == userId && e.Professional != null)
                .Select(e => (Guid?)e.Professional!.Id)
                .FirstOrDefaultAsync(ct)
            : null;

        _professionalIdResolved = true;
        return _professionalId;
    }
}
