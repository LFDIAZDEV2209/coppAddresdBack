namespace CoppAddresd.Telemedicine.Application.Constants;

/// <summary>
/// Códigos de permiso del módulo de citas/telemedicina (convención
/// <c>Recurso.Acción</c>, 2 partes como el resto del proyecto). Los valores usan
/// la nomenclatura nueva <c>Appointments.*</c>: el Auth Service los siembra y
/// asigna a roles (seeder) y los emite como claims (<c>permission</c>) en el
/// JWT. Durante la transición dual-emit, un grant del código legado
/// <c>Telemedicine.*</c> también emite su equivalente <c>Appointments.*</c>, así
/// que los usuarios con permisos legados siguen autorizando aquí.
/// </summary>
public static class AppointmentPermissionCodes
{
    public const string RequestsCreate = "Appointments.RequestsCreate";
    public const string RequestsView = "Appointments.RequestsView";
    public const string RequestsConfirm = "Appointments.RequestsConfirm";

    public const string AppointmentsSchedule = "Appointments.Schedule";
    public const string AppointmentsView = "Appointments.View";
    public const string AppointmentsCancel = "Appointments.Cancel";
    public const string AppointmentsReschedule = "Appointments.Reschedule";

    public const string AgendaView = "Appointments.AgendaView";

    /// <summary>
    /// Vista administrativa global: listados de citas, solicitudes, sesiones y
    /// KPIs del admin. Solo roles administrativos (OrgAdmin/ClinicAdmin); los
    /// profesionales usan su agenda por identidad.
    /// </summary>
    public const string AdminView = "Appointments.AdminView";

    /// <summary>
    /// Bandeja de alertas. Los profesionales ven SOLO sus propias alertas por
    /// identidad (JWT); este permiso habilita la vista administrativa global
    /// (todas las alertas, filtrar por clínica es evolución futura).
    /// </summary>
    public const string AlertsView = "Appointments.AlertsView";

    /// <summary>
    /// Supervisión de salas/sesiones: permite unirse, iniciar y finalizar la
    /// sesión de CUALQUIER cita (supervisor clínico/admin). Los profesionales y
    /// pacientes de la cita acceden por su identidad (JWT), sin necesitar este
    /// permiso.
    /// </summary>
    public const string SessionsManage = "Appointments.SessionsManage";

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
        AlertsView,
        SessionsManage,
        AdminView,
    ];
}
