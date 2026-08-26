namespace CoppAddresd.Auth.Constants;

/// <summary>
/// Mapa de transición dual-emit entre los códigos de permiso legados
/// (Telemedicine.*, deprecados) y sus equivalentes nuevos (Appointments.*).
/// Es la ÚNICA fuente de la correspondencia: la usan TokenService (dual-emit en
/// JWTs) y ScopedPermissionService (dual-check en la autorización scoped).
/// Cuando la migración a Appointments.* termine (fases posteriores), este mapa
/// y los códigos legados se eliminarán juntos.
/// </summary>
public static class PermissionCodeMap
{
    private static readonly IReadOnlyDictionary<string, string> OldToNew =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PermissionCodes.TelemedicineRequestsCreate] = PermissionCodes.AppointmentsRequestsCreate,
            [PermissionCodes.TelemedicineRequestsView] = PermissionCodes.AppointmentsRequestsView,
            [PermissionCodes.TelemedicineRequestsConfirm] = PermissionCodes.AppointmentsRequestsConfirm,
            [PermissionCodes.TelemedicineAppointmentsSchedule] = PermissionCodes.AppointmentsSchedule,
            [PermissionCodes.TelemedicineAppointmentsView] = PermissionCodes.AppointmentsView,
            [PermissionCodes.TelemedicineAppointmentsCancel] = PermissionCodes.AppointmentsCancel,
            [PermissionCodes.TelemedicineAppointmentsReschedule] = PermissionCodes.AppointmentsReschedule,
            [PermissionCodes.TelemedicineAgendaView] = PermissionCodes.AppointmentsAgendaView,
            [PermissionCodes.TelemedicineAlertsView] = PermissionCodes.AppointmentsAlertsView,
            [PermissionCodes.TelemedicineSessionsManage] = PermissionCodes.AppointmentsSessionsManage,
            [PermissionCodes.TelemedicineAdminView] = PermissionCodes.AppointmentsAdminView,
        };

    private static readonly IReadOnlyDictionary<string, string> NewToOld =
        OldToNew.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>
    /// Si el código es legado (Telemedicine.*), devuelve su equivalente nuevo
    /// (Appointments.*). Úsalo para el dual-emit: cada grant legado también
    /// emite el código nuevo en el JWT.
    /// </summary>
    public static bool TryGetNewCode(string permissionCode, out string newCode)
    {
        if (OldToNew.TryGetValue(permissionCode, out var mapped))
        {
            newCode = mapped;
            return true;
        }

        newCode = string.Empty;
        return false;
    }

    /// <summary>
    /// Si el código es nuevo (Appointments.*), devuelve su equivalente legado
    /// (Telemedicine.*). Úsalo para el dual-check: una petición con el código
    /// nuevo también acepta el grant del código legado como fallback.
    /// </summary>
    public static bool TryGetLegacyCode(string permissionCode, out string legacyCode)
    {
        if (NewToOld.TryGetValue(permissionCode, out var mapped))
        {
            legacyCode = mapped;
            return true;
        }

        legacyCode = string.Empty;
        return false;
    }
}