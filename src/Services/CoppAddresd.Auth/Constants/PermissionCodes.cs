namespace CoppAddresd.Auth.Constants;

public static class PermissionCodes
{
    public const string UsersView = "Users.View";
    public const string UsersCreate = "Users.Create";
    public const string UsersUpdate = "Users.Update";
    public const string UsersDelete = "Users.Delete";

    public const string RolesView = "Roles.View";
    public const string RolesCreate = "Roles.Create";
    public const string RolesUpdate = "Roles.Update";
    public const string RolesDelete = "Roles.Delete";
    public const string RolesAssign = "Roles.Assign";

    public const string PermissionsView = "Permissions.View";
    public const string PermissionsAssign = "Permissions.Assign";

    public const string AgentsView = "Agents.View";
    public const string AgentsCreate = "Agents.Create";
    public const string AgentsUpdate = "Agents.Update";
    public const string AgentsDelete = "Agents.Delete";

    public const string OrganizationsView = "Organizations.View";
    public const string OrganizationsCreate = "Organizations.Create";
    public const string OrganizationsUpdate = "Organizations.Update";
    public const string OrganizationsDelete = "Organizations.Delete";

    public const string ClinicsView = "Clinics.View";
    public const string ClinicsCreate = "Clinics.Create";
    public const string ClinicsUpdate = "Clinics.Update";
    public const string ClinicsDelete = "Clinics.Delete";

    public const string LocationsView = "Locations.View";
    public const string LocationsCreate = "Locations.Create";
    public const string LocationsUpdate = "Locations.Update";
    public const string LocationsDelete = "Locations.Delete";

    public const string EmployeesView = "Employees.View";
    public const string EmployeesCreate = "Employees.Create";
    public const string EmployeesUpdate = "Employees.Update";
    public const string EmployeesDelete = "Employees.Delete";

    public const string ProfessionalsView = "Professionals.View";
    public const string ProfessionalsCreate = "Professionals.Create";
    public const string ProfessionalsUpdate = "Professionals.Update";
    public const string ProfessionalsDelete = "Professionals.Delete";

    public const string PatientsView = "Patients.View";
    public const string PatientsCreate = "Patients.Create";
    public const string PatientsUpdate = "Patients.Update";
    public const string PatientsDelete = "Patients.Delete";
    public const string PatientsExport = "Patients.Export";
    public const string PatientsBulkUpdate = "Patients.BulkUpdate";

    /// <summary>Alcance de datos "propios": solo los pacientes asignados al profesional autenticado (data scope por identidad).</summary>
    public const string PatientsViewOwn = "Patients.ViewOwn";

    /// <summary>Recetario: ver recetas y pacientes del ámbito propio.</summary>
    public const string PrescriptionsView = "Prescriptions.View";

    /// <summary>Recetario: crear recetas.</summary>
    public const string PrescriptionsCreate = "Prescriptions.Create";

    public const string DocumentsView = "Documents.View";
    public const string DocumentsUpload = "Documents.Upload";
    public const string DocumentsDelete = "Documents.Delete";
    public const string DocumentsUpdate = "Documents.Update";

    public const string ClinicalRecordsView = "ClinicalRecords.View";
    public const string ClinicalRecordsCreate = "ClinicalRecords.Create";
    public const string ClinicalRecordsUpdate = "ClinicalRecords.Update";

    // [DEPRECATED] mantener durante transición dual-emit: estos códigos
    // Telemedicine.* permanecen funcionales (se siguen emitiendo en los JWTs y
    // aceptando en la autorización) hasta que todos los módulos migren a
    // Appointments.* (fases posteriores del rename). NO eliminar ni renombrar:
    // hay grants persistidos (auth.RolePermissions / auth.UserPermissions /
    // auth.ScopedPermissionAssignments) que aún los referencian.
    // Telemedicina (módulo consumido por el microservicio de Telemedicina).
    public const string TelemedicineRequestsCreate = "Telemedicine.RequestsCreate";
    public const string TelemedicineRequestsView = "Telemedicine.RequestsView";
    public const string TelemedicineRequestsConfirm = "Telemedicine.RequestsConfirm";
    public const string TelemedicineAppointmentsSchedule = "Telemedicine.AppointmentsSchedule";
    public const string TelemedicineAppointmentsView = "Telemedicine.AppointmentsView";
    public const string TelemedicineAppointmentsCancel = "Telemedicine.AppointmentsCancel";
    public const string TelemedicineAppointmentsReschedule = "Telemedicine.AppointmentsReschedule";
    public const string TelemedicineAgendaView = "Telemedicine.AgendaView";
    public const string TelemedicineAlertsView = "Telemedicine.AlertsView";
    public const string TelemedicineSessionsManage = "Telemedicine.SessionsManage";

    /// <summary>Vista administrativa global (listados de citas, solicitudes, sesiones y KPIs).</summary>
    public const string TelemedicineAdminView = "Telemedicine.AdminView";

    // Citas genéricas (Appointments.*): reemplazan a Telemedicine.* durante la
    // transición dual-emit. Un grant del código legado equivale al nuevo (ver
    // PermissionCodeMap) y los JWTs emiten ambos códigos.
    public const string AppointmentsRequestsCreate = "Appointments.RequestsCreate";
    public const string AppointmentsRequestsView = "Appointments.RequestsView";
    public const string AppointmentsRequestsConfirm = "Appointments.RequestsConfirm";
    public const string AppointmentsSchedule = "Appointments.Schedule";
    public const string AppointmentsView = "Appointments.View";
    public const string AppointmentsCancel = "Appointments.Cancel";
    public const string AppointmentsReschedule = "Appointments.Reschedule";
    public const string AppointmentsAgendaView = "Appointments.AgendaView";
    public const string AppointmentsAlertsView = "Appointments.AlertsView";
    public const string AppointmentsSessionsManage = "Appointments.SessionsManage";

    /// <summary>Vista administrativa global (listados de citas, solicitudes, sesiones y KPIs).</summary>
    public const string AppointmentsAdminView = "Appointments.AdminView";

    // Módulos de la plataforma sin flujo clínico (visibilidad de navegación y
    // acceso futuro de sus endpoints): el profesional clínico no los tiene.
    public const string InventoryView = "Inventory.View";
    public const string StoreView = "Store.View";
    public const string MediaView = "Media.View";
    public const string AuditView = "Audit.View";

    /// <summary>Configuraciones administrativas del módulo Sistema (IA, integraciones, etc.).</summary>
    public const string SystemAdminSettings = "System.AdminSettings";

    // Wellness
    public const string WellnessView = "Wellness.View";
    public const string WellnessManage = "Wellness.Manage";

    // Store
    public const string StoreManage = "Store.Manage";

    // Community
    public const string CommunityView = "Community.View";
    public const string CommunityModerate = "Community.Moderate";
    public const string CommunityProfiles = "Community.Profiles";
    public const string CommunityManage = "Community.Manage";

    // Documentos Legales
    public const string LegalDocumentsView = "LegalDocuments.View";
    public const string LegalDocumentsManage = "LegalDocuments.Manage";

    // Financiero: acceso a información financiera sin acceso clínico.
    public const string FinanceView = "Finance.View";
    public const string FinanceManage = "Finance.Manage";

    // Reportes: lectura de reportes/analítica del ERP.
    public const string ReportsView = "Reports.View";

    // Tests de Salud: catálogo + asignación + revisión + lectura (global/own).
    public const string HealthTestsView = "HealthTests.View";
    public const string HealthTestsViewOwn = "HealthTests.ViewOwn";
    public const string HealthTestsManage = "HealthTests.Manage";
    public const string HealthTestsAssign = "HealthTests.Assign";
    public const string HealthTestsReview = "HealthTests.Review";

    public static IEnumerable<string> GetAll()
    {
        yield return UsersView;
        yield return UsersCreate;
        yield return UsersUpdate;
        yield return UsersDelete;
        yield return RolesView;
        yield return RolesCreate;
        yield return RolesUpdate;
        yield return RolesDelete;
        yield return RolesAssign;
        yield return PermissionsView;
        yield return PermissionsAssign;
        yield return AgentsView;
        yield return AgentsCreate;
        yield return AgentsUpdate;
        yield return AgentsDelete;
        yield return OrganizationsView;
        yield return OrganizationsCreate;
        yield return OrganizationsUpdate;
        yield return OrganizationsDelete;
        yield return ClinicsView;
        yield return ClinicsCreate;
        yield return ClinicsUpdate;
        yield return ClinicsDelete;
        yield return LocationsView;
        yield return LocationsCreate;
        yield return LocationsUpdate;
        yield return LocationsDelete;
        yield return EmployeesView;
        yield return EmployeesCreate;
        yield return EmployeesUpdate;
        yield return EmployeesDelete;
        yield return ProfessionalsView;
        yield return ProfessionalsCreate;
        yield return ProfessionalsUpdate;
        yield return ProfessionalsDelete;
        yield return PatientsView;
        yield return PatientsCreate;
        yield return PatientsUpdate;
        yield return PatientsDelete;
        yield return PatientsExport;
        yield return PatientsBulkUpdate;
        yield return PatientsViewOwn;
        yield return PrescriptionsView;
        yield return PrescriptionsCreate;
        yield return DocumentsView;
        yield return DocumentsUpload;
        yield return DocumentsDelete;
        yield return DocumentsUpdate;
        yield return ClinicalRecordsView;
        yield return ClinicalRecordsCreate;
        yield return ClinicalRecordsUpdate;
        yield return TelemedicineRequestsCreate;
        yield return TelemedicineRequestsView;
        yield return TelemedicineRequestsConfirm;
        yield return TelemedicineAppointmentsSchedule;
        yield return TelemedicineAppointmentsView;
        yield return TelemedicineAppointmentsCancel;
        yield return TelemedicineAppointmentsReschedule;
        yield return TelemedicineAgendaView;
        yield return TelemedicineAlertsView;
        yield return TelemedicineSessionsManage;
        yield return TelemedicineAdminView;
        yield return AppointmentsRequestsCreate;
        yield return AppointmentsRequestsView;
        yield return AppointmentsRequestsConfirm;
        yield return AppointmentsSchedule;
        yield return AppointmentsView;
        yield return AppointmentsCancel;
        yield return AppointmentsReschedule;
        yield return AppointmentsAgendaView;
        yield return AppointmentsAlertsView;
        yield return AppointmentsSessionsManage;
        yield return AppointmentsAdminView;
        yield return InventoryView;
        yield return StoreView;
        yield return StoreManage;
        yield return MediaView;
        yield return AuditView;
        yield return SystemAdminSettings;
        yield return CommunityView;
        yield return CommunityModerate;
        yield return CommunityProfiles;
        yield return CommunityManage;
        yield return WellnessView;
        yield return WellnessManage;
        yield return LegalDocumentsView;
        yield return LegalDocumentsManage;
        yield return FinanceView;
        yield return FinanceManage;
        yield return ReportsView;
        yield return HealthTestsView;
        yield return HealthTestsViewOwn;
        yield return HealthTestsManage;
        yield return HealthTestsAssign;
        yield return HealthTestsReview;
    }

    public static string GetModule(string permissionCode)
    {
        return permissionCode.Split('.')[0];
    }

    public static string GetAction(string permissionCode)
    {
        return permissionCode.Split('.')[1];
    }
}
