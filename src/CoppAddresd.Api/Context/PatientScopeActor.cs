using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.Api.Context;

/// <summary>Adapta el contexto ERP al guard de aplicación; solo el rol Admin exceptúa alcance.</summary>
public sealed class PatientScopeActor(ICurrentContext context, IHttpContextAccessor http) : IPatientScopeActor
{
    public Guid? UserId => context.UserId;
    public bool IsAdmin => http.HttpContext?.User.IsInRole("Admin") == true;
    public Task<Guid?> ProfessionalIdAsync(CancellationToken ct) => context.GetProfessionalIdAsync(ct);
    public Task<bool> HasPermissionAsync(string permission, CancellationToken ct) => context.HasPermissionAsync(permission, ct);
}
