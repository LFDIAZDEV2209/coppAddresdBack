using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Seeders;

/// <summary>
/// Siembra los roles del ERP por contexto (OrganizationAdmin, ClinicAdmin,
/// ClinicalDirector, Physician, Nutritionist, Psychologist, Nurse,
/// Receptionist, CareCoordinator) con sus permisos por defecto. El rol Admin
/// (global) se maneja en <see cref="AdminSeeder"/> con todos los permisos.
/// Estos roles se asignan a usuarios con scope (clínica/organización) vía las
/// asignaciones scoped; los permisos aquí definidos son la base editable por
/// el administrador desde la UI.
/// </summary>
public static class RoleSeeder
{
    public static async Task SeedAsync(AuthDbContext dbContext, ILogger logger, CancellationToken ct = default)
    {
        logger.LogInformation("Seeding organizational roles...");

        foreach (var (roleName, description, permissionCodes) in DefaultRoles)
        {
            var role = await dbContext.Roles
                .FirstOrDefaultAsync(r => r.Name == roleName, ct);

            if (role is null)
            {
                role = new ApplicationRole
                {
                    Name = roleName,
                    Description = description,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                };
                dbContext.Roles.Add(role);
                await dbContext.SaveChangesAsync(ct);
                logger.LogInformation("Rol creado: {Role}", roleName);
            }

            // Permisos por defecto (idempotente): solo agrega los que faltan.
            var assignedIds = await dbContext.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.PermissionId)
                .ToListAsync(ct);

            var permissionIds = await dbContext.Permissions
                .Where(p => permissionCodes.Contains(p.Code))
                .Select(p => new { p.Id, p.Code })
                .ToListAsync(ct);

            var toAssign = permissionIds
                .Where(p => !assignedIds.Contains(p.Id))
                .Select(p => new RolePermission { RoleId = role.Id, PermissionId = p.Id })
                .ToList();

            if (toAssign.Count > 0)
            {
                dbContext.RolePermissions.AddRange(toAssign);
                await dbContext.SaveChangesAsync(ct);
                logger.LogInformation("Rol {Role}: asignados {Count} permisos por defecto", roleName, toAssign.Count);
            }
        }
    }

    /// <summary>Permisos de telemedicina por perfil (declarados ANTES de DefaultRoles:
    /// los collection expressions con spread evalúan al inicializar el campo).</summary>
    private static readonly string[] AllTelemedicinePermissions =
    [
        PermissionCodes.TelemedicineRequestsCreate,
        PermissionCodes.TelemedicineRequestsView,
        PermissionCodes.TelemedicineRequestsConfirm,
        PermissionCodes.TelemedicineAppointmentsSchedule,
        PermissionCodes.TelemedicineAppointmentsView,
        PermissionCodes.TelemedicineAppointmentsCancel,
        PermissionCodes.TelemedicineAppointmentsReschedule,
        PermissionCodes.TelemedicineAgendaView,
        PermissionCodes.TelemedicineAlertsView,
        // Supervisión de salas/sesiones: lo tienen los roles administrativos
        // (OrgAdmin/ClinicAdmin vía este array y ClinicalDirector explícito).
        // Los profesionales de línea NO: acceden a su sala por identidad (JWT).
        PermissionCodes.TelemedicineSessionsManage,
        // Vista administrativa global (listados de citas/solicitudes/sesiones y KPIs).
        PermissionCodes.TelemedicineAdminView,
    ];

    private static readonly string[] ProfessionalTelemedicinePermissions =
    [
        PermissionCodes.TelemedicineRequestsView,
        PermissionCodes.TelemedicineRequestsConfirm,
        PermissionCodes.TelemedicineAppointmentsSchedule,
        PermissionCodes.TelemedicineAppointmentsView,
        PermissionCodes.TelemedicineAppointmentsCancel,
        PermissionCodes.TelemedicineAppointmentsReschedule,
        PermissionCodes.TelemedicineAgendaView,
        PermissionCodes.TelemedicineAlertsView,
    ];

    private static readonly string[] StaffTelemedicinePermissions =
    [
        PermissionCodes.TelemedicineRequestsCreate,
        PermissionCodes.TelemedicineRequestsView,
        PermissionCodes.TelemedicineAppointmentsSchedule,
        PermissionCodes.TelemedicineAppointmentsView,
        PermissionCodes.TelemedicineAppointmentsCancel,
        PermissionCodes.TelemedicineAppointmentsReschedule,
        PermissionCodes.TelemedicineAgendaView,
    ];

    private static readonly string[] ViewerTelemedicinePermissions =
    [
        PermissionCodes.TelemedicineRequestsView,
        PermissionCodes.TelemedicineAppointmentsView,
        PermissionCodes.TelemedicineAgendaView,
    ];

    /// <summary>Rol → (descripción, códigos de permiso por defecto).</summary>
    private static readonly (string Role, string Description, string[] Permissions)[] DefaultRoles =
    [
        ("OrganizationAdmin", "Administra la organización: estructura, empleados y profesionales",
            [
                PermissionCodes.OrganizationsView, PermissionCodes.OrganizationsCreate,
                PermissionCodes.OrganizationsUpdate, PermissionCodes.OrganizationsDelete,
                PermissionCodes.ClinicsView, PermissionCodes.ClinicsCreate,
                PermissionCodes.ClinicsUpdate, PermissionCodes.ClinicsDelete,
                PermissionCodes.LocationsView, PermissionCodes.LocationsCreate,
                PermissionCodes.LocationsUpdate, PermissionCodes.LocationsDelete,
                PermissionCodes.EmployeesView, PermissionCodes.EmployeesCreate,
                PermissionCodes.EmployeesUpdate, PermissionCodes.EmployeesDelete,
                PermissionCodes.ProfessionalsView, PermissionCodes.ProfessionalsCreate,
                PermissionCodes.ProfessionalsUpdate, PermissionCodes.ProfessionalsDelete,
                PermissionCodes.PatientsView, PermissionCodes.PatientsCreate,
                PermissionCodes.PatientsUpdate, PermissionCodes.PatientsDelete,
                PermissionCodes.PatientsExport, PermissionCodes.PatientsBulkUpdate,
                PermissionCodes.DocumentsView, PermissionCodes.DocumentsUpload, PermissionCodes.DocumentsUpdate, PermissionCodes.DocumentsDelete,
                PermissionCodes.ClinicalRecordsView, PermissionCodes.ClinicalRecordsCreate,
                PermissionCodes.ClinicalRecordsUpdate,
                ..AllTelemedicinePermissions,
            ]),
        ("ClinicAdmin", "Administra una clínica y sus sedes",
            [
                PermissionCodes.ClinicsView, PermissionCodes.ClinicsUpdate,
                PermissionCodes.LocationsView, PermissionCodes.LocationsCreate,
                PermissionCodes.LocationsUpdate,
                PermissionCodes.EmployeesView, PermissionCodes.EmployeesCreate,
                PermissionCodes.EmployeesUpdate,
                PermissionCodes.ProfessionalsView, PermissionCodes.ProfessionalsCreate,
                PermissionCodes.ProfessionalsUpdate,
                PermissionCodes.PatientsView, PermissionCodes.PatientsCreate,
                PermissionCodes.PatientsUpdate,
                PermissionCodes.DocumentsView, PermissionCodes.DocumentsUpload, PermissionCodes.DocumentsUpdate, PermissionCodes.ClinicalRecordsView, PermissionCodes.ClinicalRecordsCreate,
                PermissionCodes.ClinicalRecordsUpdate,
                ..AllTelemedicinePermissions,
            ]),
        ("ClinicalDirector", "Dirección clínica: supervisa historiales y profesionales",
            [
                PermissionCodes.PatientsView, PermissionCodes.PatientsUpdate,
                PermissionCodes.DocumentsView,
                PermissionCodes.ClinicalRecordsView, PermissionCodes.ClinicalRecordsCreate,
                PermissionCodes.ClinicalRecordsUpdate,
                PermissionCodes.ProfessionalsView,
                PermissionCodes.EmployeesView,
                PermissionCodes.TelemedicineSessionsManage,
                ..ProfessionalTelemedicinePermissions,
            ]),
        ("Physician", "Médico: atiende pacientes y registra historia clínica",
            [
                PermissionCodes.PatientsView, PermissionCodes.PatientsCreate,
                PermissionCodes.PatientsUpdate,
                PermissionCodes.DocumentsView, PermissionCodes.DocumentsUpload, PermissionCodes.DocumentsUpdate, PermissionCodes.ClinicalRecordsView, PermissionCodes.ClinicalRecordsCreate,
                PermissionCodes.ClinicalRecordsUpdate,
                PermissionCodes.ProfessionalsView,
                ..ProfessionalTelemedicinePermissions,
            ]),
        ("Nutritionist", "Nutricionista: manejo de nutrición y pacientes",
            [
                PermissionCodes.PatientsView, PermissionCodes.PatientsCreate,
                PermissionCodes.PatientsUpdate,
                PermissionCodes.DocumentsView, PermissionCodes.DocumentsUpload, PermissionCodes.DocumentsUpdate, PermissionCodes.ClinicalRecordsView, PermissionCodes.ClinicalRecordsCreate,
                PermissionCodes.ClinicalRecordsUpdate,
                PermissionCodes.ProfessionalsView,
                ..ProfessionalTelemedicinePermissions,
            ]),
        ("Psychologist", "Psicólogo: salud conductual y pacientes",
            [
                PermissionCodes.PatientsView, PermissionCodes.PatientsCreate,
                PermissionCodes.PatientsUpdate,
                PermissionCodes.DocumentsView, PermissionCodes.DocumentsUpload, PermissionCodes.DocumentsUpdate, PermissionCodes.ClinicalRecordsView, PermissionCodes.ClinicalRecordsCreate,
                PermissionCodes.ClinicalRecordsUpdate,
                PermissionCodes.ProfessionalsView,
                ..ProfessionalTelemedicinePermissions,
            ]),
        ("Nurse", "Enfermería: soporte clínico y registro",
            [
                PermissionCodes.PatientsView, PermissionCodes.PatientsUpdate,
                PermissionCodes.DocumentsView, PermissionCodes.DocumentsUpload, PermissionCodes.DocumentsUpdate, PermissionCodes.ClinicalRecordsView, PermissionCodes.ClinicalRecordsCreate,
                PermissionCodes.ClinicalRecordsUpdate,
                ..ViewerTelemedicinePermissions,
            ]),
        ("Receptionist", "Recepción: agenda, registro de pacientes y documentos",
            [
                PermissionCodes.PatientsView, PermissionCodes.PatientsCreate,
                PermissionCodes.DocumentsView, PermissionCodes.DocumentsUpload, PermissionCodes.DocumentsUpdate, PermissionCodes.ProfessionalsView,
                ..StaffTelemedicinePermissions,
            ]),
        ("CareCoordinator", "Coordinación de cuidados: seguimiento del paciente",
            [
                PermissionCodes.PatientsView,
                PermissionCodes.DocumentsView,
                PermissionCodes.ClinicalRecordsView,
                PermissionCodes.ProfessionalsView,
                ..ViewerTelemedicinePermissions,
            ]),
    ];
}


