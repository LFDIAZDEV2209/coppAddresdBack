using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CoppAddresd.Api.Context;

/// <summary>
/// Resolución del actor del módulo Progreso del Programa (SPEC §6.14 y PLAN
/// OQ-1): el paciente se deriva SIEMPRE de la identidad del JWT, nunca del
/// body. El JWT puede transportar un claim <c>patient_id</c> (emitido por el
/// Auth Service en el futuro); si no lo trae, se resuelve el perfil con una
/// sola consulta a <c>app.patient_profiles.user_id</c> (memoizada por request,
/// cache-friendly).
///
/// Es la base del anti-IDOR (AC-11): toda lectura/escritura de un paciente se
/// valida contra esta identidad, y cualquier cruce devuelve 404 (nunca 403).
/// </summary>
public interface IProgramActorContext
{
    /// <summary>Id del usuario en <c>auth.users</c> (claim <c>sub</c> del JWT).</summary>
    Guid? UserId { get; }

    /// <summary>
    /// Roles del usuario autenticado (claims <c>role</c> del JWT, pueden ser
    /// varios). Los usan las guardias clínicas del módulo (AC-22 y SPEC §15, D:
    /// solo <c>Physician</c>/<c>Nutritionist</c>/<c>Psychologist</c>/
    /// <c>ClinicalDirector</c>/<c>Admin</c> deciden una revisión clínica de XP).
    /// </summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// Id del perfil de paciente (<c>app.patient_profiles.id</c>) del usuario
    /// autenticado, o null si el usuario no tiene perfil. Memoizado por request.
    /// </summary>
    Task<Guid?> ResolvePatientProfileIdAsync(CancellationToken ct = default);

    /// <summary>
    /// Id de la inscripción activa del paciente (una por paciente, índice único
    /// parcial), o null si no hay. La usan snapshot/calendario/sendero, cuyas
    /// rutas no reciben <c>enrollmentId</c> del cliente (SPEC §7.1/§7.3/§7.4).
    /// </summary>
    Task<Guid?> ResolveActiveEnrollmentIdAsync(CancellationToken ct = default);

    /// <summary>
    /// ¿Pertenece la inscripción al paciente autenticado? Falso si el paciente
    /// no tiene perfil o la inscripción es de otro paciente (anti-IDOR, AC-11).
    /// </summary>
    Task<bool> EnrollmentBelongsToCurrentPatientAsync(Guid enrollmentId, CancellationToken ct = default);

    /// <summary>
    /// Scoping clínico del actor sobre una inscripción (T-81, SPEC §6.14):
    /// TRUE si el actor es Admin/OrganizationAdmin/ClinicAdmin (alcance
    /// org/clinic), el paciente dueño de la inscripción, o un profesional con
    /// asignación activa en <c>app.patient_professionals</c>. Cualquier otro
    /// caso → FALSE (el endpoint responde 404, nunca 403).
    /// </summary>
    Task<bool> ActorScopedToEnrollmentAsync(Guid enrollmentId, CancellationToken ct = default);

    /// <summary>
    /// Ids de paciente visibles por el actor (T-81): <c>null</c> = sin filtro
    /// (bypass para roles Admin/OrganizationAdmin/ClinicAdmin); lista con ids =
    /// filtro IN (paciente: la propia; clínico: los asignados activos); lista
    /// VACÍA = denegar (sin identidad conocida o clínico sin asignaciones).
    /// </summary>
    Task<IReadOnlyList<Guid>?> ResolveScopedPatientIdsAsync(CancellationToken ct = default);
}

/// <summary>
/// Implementación con <c>ICurrentContext</c> (claims JWT) + una consulta
/// <c>AsNoTracking</c> a <c>app.patient_profiles</c> cuando el token no trae
/// <c>patient_id</c>. Todas las resoluciones se memoizan por request.
/// </summary>
public sealed class ProgramActorContext(
    ICurrentContext currentContext,
    IHttpContextAccessor httpContextAccessor,
    AppDbContext dbContext) : IProgramActorContext
{
    private const string PatientIdClaim = "patient_id";
    private const string PatientIdClaimAlt = "patientId";

    private Guid? _patientProfileId;
    private bool _patientProfileResolved;
    private Guid? _activeEnrollmentId;
    private bool _activeEnrollmentResolved;
    private readonly Dictionary<Guid, bool> _enrollmentScopeCache = new();
    private IReadOnlyList<Guid>? _scopedPatientIds;
    private bool _scopedPatientIdsResolved;
    private Guid? _professionalId;
    private bool _professionalResolved;

    /// <summary>
    /// Roles con alcance de administración org/clinic: NO se filtran por
    /// patient_professionals (el IDOR a prevenir es entre clínicos, T-81).
    /// </summary>
    private static readonly HashSet<string> ScopeBypassRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "OrganizationAdmin",
        "ClinicAdmin",
    };

    public Guid? UserId => currentContext.UserId;

    /// <summary>
    /// Roles del JWT (claim <c>role</c>): el Auth Service emite uno por rol
    /// asignado (TokenService). Vacío si no hay contexto HTTP autenticado.
    /// </summary>
    public IReadOnlyList<string> Roles
        => httpContextAccessor.HttpContext?.User
            .FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

    public async Task<Guid?> ResolvePatientProfileIdAsync(CancellationToken ct = default)
    {
        if (_patientProfileResolved)
        {
            return _patientProfileId;
        }

        _patientProfileResolved = true;
        _patientProfileId = await ResolvePatientProfileIdCoreAsync(ct);
        return _patientProfileId;
    }

    public async Task<Guid?> ResolveActiveEnrollmentIdAsync(CancellationToken ct = default)
    {
        if (_activeEnrollmentResolved)
        {
            return _activeEnrollmentId;
        }

        _activeEnrollmentResolved = true;
        _activeEnrollmentId = await ResolveActiveEnrollmentIdCoreAsync(ct);
        return _activeEnrollmentId;
    }

    public async Task<bool> EnrollmentBelongsToCurrentPatientAsync(
        Guid enrollmentId, CancellationToken ct = default)
    {
        var patientId = await ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return false;
        }

        return await dbContext.ProgramEnrollments.AsNoTracking()
            .AnyAsync(e => e.Id == enrollmentId && e.PatientId == patientId, ct);
    }

    /// <inheritdoc />
    public async Task<bool> ActorScopedToEnrollmentAsync(
        Guid enrollmentId, CancellationToken ct = default)
    {
        if (_enrollmentScopeCache.TryGetValue(enrollmentId, out var cached))
        {
            return cached;
        }

        var patientId = await dbContext.ProgramEnrollments.AsNoTracking()
            .Where(e => e.Id == enrollmentId)
            .Select(e => (Guid?)e.PatientId)
            .FirstOrDefaultAsync(ct);

        // Inscripción inexistente → el llamador responde 404 igualmente.
        var allowed = patientId is not null && await IsScopedToPatientAsync(patientId.Value, ct);
        _enrollmentScopeCache[enrollmentId] = allowed;
        return allowed;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>?> ResolveScopedPatientIdsAsync(CancellationToken ct = default)
    {
        if (_scopedPatientIdsResolved)
        {
            return _scopedPatientIds;
        }

        if (Roles.Any(role => ScopeBypassRoles.Contains(role)))
        {
            // Roles de administración con alcance org/clinic: SIN filtro (null).
            _scopedPatientIds = null;
        }
        else if (await ResolvePatientProfileIdAsync(ct) is { } selfPatient)
        {
            // Paciente autenticado: solo su propio perfil.
            _scopedPatientIds = [selfPatient];
        }
        else if (await ResolveProfessionalIdAsync(ct) is { } professionalId)
        {
            // Clínico: solo pacientes con asignación activa (patient_professionals).
            // Lista VACÍA = clínico sin asignaciones → denegar.
            _scopedPatientIds = await dbContext.PatientProfessionalAssignments.AsNoTracking()
                .Where(pp => pp.ProfessionalId == professionalId && pp.Status == "Active")
                .Select(pp => pp.PatientId)
                .Distinct()
                .ToListAsync(ct);
        }
        else
        {
            // Sin identidad conocida: denegar (lista vacía).
            _scopedPatientIds = [];
        }

        _scopedPatientIdsResolved = true;
        return _scopedPatientIds;
    }

    private async Task<bool> IsScopedToPatientAsync(Guid patientId, CancellationToken ct)
    {
        if (Roles.Any(role => ScopeBypassRoles.Contains(role)))
        {
            return true;
        }

        // El paciente autenticado siempre tiene alcance sobre su propio perfil.
        var profileId = await ResolvePatientProfileIdAsync(ct);
        if (profileId == patientId)
        {
            return true;
        }

        // Clínico: asignación activa en app.patient_professionals vía
        // erp.employees.user_id → erp.professionals.employee_id.
        var professionalId = await ResolveProfessionalIdAsync(ct);
        if (professionalId is null)
        {
            return false;
        }

        return await dbContext.PatientProfessionalAssignments.AsNoTracking()
            .AnyAsync(
                pp => pp.ProfessionalId == professionalId
                    && pp.PatientId == patientId
                    && pp.Status == "Active",
                ct);
    }

    private async Task<Guid?> ResolveProfessionalIdAsync(CancellationToken ct)
    {
        if (_professionalResolved)
        {
            return _professionalId;
        }

        if (UserId is not { } userId)
        {
            _professionalId = null;
        }
        else
        {
            _professionalId = await (
                from employee in dbContext.Employees.AsNoTracking()
                join professional in dbContext.Professionals.AsNoTracking()
                    on employee.Id equals professional.EmployeeId
                where employee.UserId == userId
                select professional.Id).FirstOrDefaultAsync(ct);
        }

        _professionalResolved = true;
        return _professionalId;
    }

    private async Task<Guid?> ResolvePatientProfileIdCoreAsync(CancellationToken ct)
    {
        if (UserId is not { } userId)
        {
            return null;
        }

        // 1) Claim patient_id del JWT (PLAN OQ-1): si el Auth Service lo emite,
        //    evita la consulta. Acepta patient_id o patientId (nombres vistos en
        //    la práctica); parseo seguro, nunca se confía en el body.
        var claimValue = httpContextAccessor.HttpContext?.User
            .FindFirstValue(PatientIdClaim)
            ?? httpContextAccessor.HttpContext?.User.FindFirstValue(PatientIdClaimAlt);
        if (Guid.TryParse(claimValue, out var fromClaim))
        {
            return fromClaim;
        }

        // 2) Fallback: app.patient_profiles.user_id (una sola consulta set).
        return await dbContext.PatientProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<Guid?> ResolveActiveEnrollmentIdCoreAsync(CancellationToken ct)
    {
        var patientId = await ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return null;
        }

        return await dbContext.ProgramEnrollments.AsNoTracking()
            .Where(e => e.PatientId == patientId && e.Status == ProgramEnrollmentStatus.Active)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(ct);
    }
}