using CoppAddresd.Api.Context;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;

namespace CoppAddresd.Api.Security;

/// <summary>Autoriza imagen y máscaras del mismo análisis antes de firmar o entregar el objeto.</summary>
public sealed class FoodPhotoAccess(IFoodAnalysisRepository analyses, IPatientScopeGuard scope,
    ICurrentContext context, IHttpContextAccessor http)
{
    public static bool IsPhotoKey(string key) => CoppAddresd.Application.Common.Storage.FoodPhotoStorageKeys.IsProtected(key);

    public async Task EnsureReadableAsync(string key, bool signedRead, CancellationToken ct)
    {
        var segments = key.Split('/');
        var token = segments.Length == 2 ? Path.GetFileNameWithoutExtension(segments[1])
            : segments.Length == 4 && segments[1] == "masks" ? segments[2] : null;
        if (!Guid.TryParse(token, out var id)) throw Missing();
        var user = http.HttpContext?.User;
        var erp = user?.HasClaim("aud", "erp") == true;
        // Una firma válida es la capacidad temporal emitida después de este mismo control.
        if (!signedRead && erp) await scope.EnsureResourceAsync(PatientResource.FoodAnalysis, id, "Nutrition.PhotosView", ct);
        var analysis = await analyses.GetByAnalysisIdAsync(id, ct);
        if (analysis is null || !signedRead && !erp && (context.UserId is null || analysis.UserId != context.UserId)) throw Missing();
        if (key != analysis.ImageKey && !analysis.Items.Any(i => i.MaskKey == key)) throw Missing();
    }

    private static NotFoundException Missing() => new("Recurso no encontrado.");
}
