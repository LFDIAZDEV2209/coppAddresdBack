namespace CoppAddresd.Telemedicine.Application.Constants;

/// <summary>
/// Códigos de permiso del módulo de Telemedicina (convención <c>Recurso.Acción</c>,
/// 2 partes como el resto del proyecto). Se emiten como claims (<c>permission</c>)
/// en el JWT del Auth Service; su siembra y asignación a roles viven en el Auth
/// Service (seeder). El microservicio solo verifica la presencia del claim.
/// </summary>
public static class TelemedicinePermissionCodes
{
    public const string RequestsCreate = "Telemedicine.RequestsCreate";
    public const string RequestsView = "Telemedicine.RequestsView";
    public const string RequestsConfirm = "Telemedicine.RequestsConfirm";

    public const string AppointmentsSchedule = "Telemedicine.AppointmentsSchedule";
    public const string AppointmentsView = "Telemedicine.AppointmentsView";
    public const string AppointmentsCancel = "Telemedicine.AppointmentsCancel";
    public const string AppointmentsReschedule = "Telemedicine.AppointmentsReschedule";

    public const string AgendaView = "Telemedicine.AgendaView";

    /// <summary>Todos los códigos del módulo (para el policy provider).</summary>
    public static readonly string[] All =
    [
        RequestsCreate,
        RequestsView,
        RequestsConfirm,
        AppointmentsSchedule,
        AppointmentsView,
        AppointmentsCancel,
        AppointmentsReschedule,
        AgendaView,
    ];
}
