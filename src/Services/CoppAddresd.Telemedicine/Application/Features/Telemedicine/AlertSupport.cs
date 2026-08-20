using CoppAddresd.Telemedicine.Application.Constants;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Reglas compartidas de la bandeja de alertas: a qué alertas accede el usuario
/// autenticado. El profesional (sin <c>Telemedicine.AlertsView</c>) accede SOLO a
/// sus propias alertas (resuelto por identidad del JWT); un usuario con
/// <c>AlertsView</c> (roles administrativos) accede a todas (vista global).
/// </summary>
internal static class AlertSupport
{
    public static bool HasAlertsViewPermission(bool hasManagePermission)
        => hasManagePermission;

    /// <summary>
    /// Id del destinatario (usuario de <c>auth.users</c>) cuya bandeja se lee,
    /// o <c>null</c> para la vista administrativa global. Si el usuario no es un
    /// profesional resoluble y no tiene permiso administrativo → 403.
    /// </summary>
    public static async Task<Guid?> ResolveRecipientUserIdAsync(
        ITelemedicineReferenceDataService referenceData,
        Guid userId,
        bool hasAlertsView,
        CancellationToken ct)
    {
        if (hasAlertsView)
        {
            return null;
        }

        // El destinatario de las alertas es el usuario del JWT (auth.users), que
        // coincide con el UserId del profesional en el ERP.
        var professional = await referenceData.GetProfessionalByUserIdAsync(userId, ct);
        if (professional is not null)
        {
            return professional.UserId ?? userId;
        }

        // Sin identidad de profesional ni permiso administrativo: no hay bandeja
        // a la que acceder (los pacientes usan la app móvil, fuera del ERP).
        throw new ForbiddenException(
            "No tienes acceso a una bandeja de alertas de telemedicina.");
    }
}
