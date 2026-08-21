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

    public const string DocumentsView = "Documents.View";
    public const string DocumentsUpload = "Documents.Upload";
    public const string DocumentsDelete = "Documents.Delete";
    public const string DocumentsUpdate = "Documents.Update";

    public const string ClinicalRecordsView = "ClinicalRecords.View";
    public const string ClinicalRecordsCreate = "ClinicalRecords.Create";
    public const string ClinicalRecordsUpdate = "ClinicalRecords.Update";

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
