using System.Security.Claims;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Application.Features.Sos;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Api.Context;

/// <summary>
/// Resolución del actor del módulo SOS (change sos-panic-real, D5). El
/// paciente se deriva SIEMPRE de la identidad del JWT (claim
/// <c>patient_id</c> o lookup por <c>app.patient_profiles.user_id</c>,
/// memoizado por request) — nunca del body. El staff (aud=erp) se resuelve
/// con su usuario, profesional y contexto activo de clínica/organización.
/// </summary>
public interface ISosActorContext
{
    /// <summary>Id del usuario en auth.users (claim sub del JWT).</summary>
    Guid? UserId { get; }

    /// <summary>Perfil del paciente autenticado (null si el JWT no es de paciente).</summary>
    Task<Guid?> ResolvePatientProfileIdAsync(CancellationToken ct = default);

    /// <summary>Actor de staff ERP (usuario, profesional y scopes activos).</summary>
    Task<SosStaffActor> ResolveStaffActorAsync(CancellationToken ct = default);
}

public sealed class SosActorContext(
    ICurrentContext currentContext,
    IHttpContextAccessor httpContextAccessor,
    CoppAddresd.Infrastructure.Persistence.AppDbContext dbContext
) : ISosActorContext
{
    private const string PatientIdClaim = "patient_id";
    private const string PatientIdClaimAlt = "patientId";

    /// <summary>
    /// Roles con alcance de administración org/clínica: no requieren
    /// asignación directa (misma convención que ProgramActorContext).
    /// </summary>
    private static readonly HashSet<string> ScopeBypassRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "OrganizationAdmin",
        "ClinicAdmin",
    };

    private Guid? _patientProfileId;
    private bool _patientProfileResolved;

    public Guid? UserId => currentContext.UserId;

    public async Task<Guid?> ResolvePatientProfileIdAsync(CancellationToken ct = default)
    {
        if (_patientProfileResolved)
        {
            return _patientProfileId;
        }

        _patientProfileResolved = true;

        var userId = currentContext.UserId;
        if (userId is null)
        {
            _patientProfileId = null;
            return null;
        }

        // 1) Claim patient_id del JWT si el Auth Service lo emite.
        var claimValue =
            httpContextAccessor.HttpContext?.User.FindFirstValue(PatientIdClaim)
            ?? httpContextAccessor.HttpContext?.User.FindFirstValue(PatientIdClaimAlt);
        if (Guid.TryParse(claimValue, out var fromClaim))
        {
            _patientProfileId = fromClaim;
            return fromClaim;
        }

        // 2) Fallback: app.patient_profiles.user_id (una consulta memoizada).
        _patientProfileId = await dbContext
            .PatientProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);
        return _patientProfileId;
    }

    public async Task<SosStaffActor> ResolveStaffActorAsync(CancellationToken ct = default)
    {
        var user = httpContextAccessor.HttpContext?.User;
        var roles =
            user?.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? [];

        return new SosStaffActor(
            UserId: currentContext.UserId ?? Guid.Empty,
            // El profesional se deriva de la identidad del JWT (erp.professionals
            // vía erp.employees), memoizado por request en CurrentContext.
            ProfessionalId: await currentContext.GetProfessionalIdAsync(ct),
            ActiveClinicId: currentContext.ActiveClinicId,
            ActiveOrganizationId: currentContext.ActiveOrganizationId,
            BypassScope: roles.Overlaps(ScopeBypassRoles)
        );
    }
}
