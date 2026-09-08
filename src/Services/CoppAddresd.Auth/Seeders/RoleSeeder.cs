using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Seeders;

/// <summary>
/// Siembra los roles del ERP por contexto (OrganizationAdmin, ClinicAdmin,
/// ClinicalDirector, Professional, Nurse, Receptionist, CareCoordinator,
/// Coordinator, Finance, Auditor) con sus permisos por defecto. El rol Admin
/// (global) se maneja en <see cref="AdminSeeder"/> con todos los permisos.
/// Estos roles se asignan a usuarios con scope (clínica/organización) vía las
/// asignaciones scoped; los permisos aquí definidos son la base editable por
/// el administrador desde la UI.
///
/// Convención de escalabilidad: roles FUNCIONALES por capacidad, no por
/// profesión. Los roles clínicos legado Physician/Nutritionist/Psychologist
/// (idénticos en permisos) quedan como aliases: se garantiza su existencia
/// (IsSystem) pero no reciben asignaciones nuevas; los profesionales nuevos
/// se asignan al rol consolidado <c>Professional</c>. Un tipo de profesional
/// o especialidad nueva NUNCA exige un rol nuevo.
/// </summary>
public static class RoleSeeder
{
    /// <summary>
    /// Permisos de módulos administrativos (inventario, tienda, contenido,
    /// auditoría y configuraciones del sistema): los roles clínicos NO los
    /// tienen (mínimo privilegio); los roles staff/admin sí, para conservar el
    /// acceso actual a los módulos.
    /// </summary>
    private static readonly string[] ModuleAdminPermissions =
    [
        PermissionCodes.InventoryView,
        PermissionCodes.StoreView,
        PermissionCodes.StoreManage,
        PermissionCodes.MediaView,
        PermissionCodes.AuditView,
        PermissionCodes.SystemAdminSettings,
    ];

    /// <summary>Recetario (solo pacientes del ámbito propio del profesional).</summary>
    private static readonly string[] PrescriberPermissions =
    [
        PermissionCodes.PrescriptionsView,
        PermissionCodes.PrescriptionsCreate,
    ];

    /// <summary>
    /// Defaults revocados por convención: códigos que dejaron de ser parte del
    /// rol por defecto y deben retirarse de asignaciones existentes (idempotente).
    /// Solo se revocan estos códigos explícitos; nunca toca asignaciones manuales
    /// de otros permisos.
    /// </summary>
    private static readonly (string Role, string[] Codes)[] RevokedDefaults =
    [
        ("Physician", [PermissionCodes.PatientsView, PermissionCodes.ProfessionalsView]),
        ("Nutritionist", [PermissionCodes.PatientsView, PermissionCodes.ProfessionalsView]),
        ("Psychologist", [PermissionCodes.PatientsView, PermissionCodes.ProfessionalsView]),
        ("Nurse", [PermissionCodes.PatientsView, PermissionCodes.ProfessionalsView]),
    ];

    /// <summary>
    /// Aliases legado: roles clínicos por profesión reemplazados por el rol
    /// consolidado <c>Professional</c>. Se garantiza su existencia e IsSystem,
    /// pero se crean y mantienen DESACTIVADOS (IsActive = false) — la cadena
    /// de permisos no filtra por IsActive, así que los usuarios ya asignados
    /// conservan sus permisos. No deben usarse para usuarios nuevos.
    /// </summary>
    private static readonly string[] LegacyAliasRoles =
    [
        "Physician",
        "Nutritionist",
        "Psychologist",
    ];

    /// <summary>
    /// Nombres de todos los roles de sistema (Admin + DefaultRoles + aliases).
    /// Se marcan IsSystem de forma idempotente al sembrar (también en BD
    /// existentes), protegiéndolos de renombrado/eliminación accidental.
    /// </summary>
    private static readonly string[] SystemRoleNames =
    [
        "Admin",
        "OrganizationAdmin",
        "ClinicAdmin",
        "ClinicalDirector",
        "Professional",
        "Physician",
        "Nutritionist",
        "Psychologist",
        "Nurse",
        "Receptionist",
        "CareCoordinator",
        "Coordinator",
        "Finance",
        "Auditor",
    ];

    public static async Task SeedAsync(
        AuthDbContext dbContext,
        ILogger logger,
        CancellationToken ct = default
    )
    {
        logger.LogInformation("Seeding organizational roles...");

        foreach (var (roleName, description, permissionCodes) in DefaultRoles)
        {
            var role = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct);

            if (role is null)
            {
                role = new ApplicationRole
                {
                    Name = roleName,
                    // Identity resuelve roles por nombre NORMALIZADO en
                    // AddToRoleAsync/FindByNameAsync; al crearlos vía DbContext
                    // (sin RoleManager) hay que normalizarlos explícitamente.
                    NormalizedName = roleName.ToUpperInvariant(),
                    Description = description,
                    IsActive = true,
                    IsSystem = true,
                    CreatedAt = DateTime.UtcNow,
                };
                dbContext.Roles.Add(role);
                await dbContext.SaveChangesAsync(ct);
                logger.LogInformation("Rol creado: {Role}", roleName);
            }

            // Permisos por defecto (idempotente): solo agrega los que faltan.
            var assignedIds = await dbContext
                .RolePermissions.Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.PermissionId)
                .ToListAsync(ct);

            var permissionIds = await dbContext
                .Permissions.Where(p => permissionCodes.Contains(p.Code))
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
                logger.LogInformation(
                    "Rol {Role}: asignados {Count} permisos por defecto",
                    roleName,
                    toAssign.Count
                );
            }
        }

        // Aliases legado: garantizar existencia (sin permisos por defecto).
        foreach (var roleName in LegacyAliasRoles)
        {
            var exists = await dbContext.Roles.AnyAsync(r => r.Name == roleName, ct);
            if (!exists)
            {
                dbContext.Roles.Add(
                    new ApplicationRole
                    {
                        Name = roleName,
                        NormalizedName = roleName.ToUpperInvariant(),
                        Description =
                            "Alias clínico legado desactivado (reemplazado por Professional); los usuarios ya asignados conservan sus permisos",
                        IsActive = false,
                        IsSystem = true,
                        CreatedAt = DateTime.UtcNow,
                    }
                );
                await dbContext.SaveChangesAsync(ct);
                logger.LogInformation("Rol legado creado (alias): {Role}", roleName);
            }
        }

        // Revocación de defaults (idempotente): aplica solo a los códigos
        // declarados en RevokedDefaults, para que el cambio de convención
        // (p. ej. Patients.View → Patients.ViewOwn) se refleje en BD existentes.
        foreach (var (roleName, codes) in RevokedDefaults)
        {
            var role = await dbContext
                .Roles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Name == roleName, ct);
            if (role is null)
            {
                continue;
            }

            var revoked = await dbContext
                .RolePermissions.Where(rp =>
                    rp.RoleId == role.Id && codes.Contains(rp.Permission.Code)
                )
                .ExecuteDeleteAsync(ct);

            if (revoked > 0)
            {
                logger.LogInformation(
                    "Rol {Role}: revocados {Count} permisos por convención de defaults",
                    roleName,
                    revoked
                );
            }
        }

        // Desactivación idempotente de aliases legado: DBs existentes quedan
        // desactivadas al arrancar; la cadena de permisos no filtra por IsActive,
        // así que nadie pierde acceso.
        var legacyDeactivated = await dbContext
            .Roles.Where(r => LegacyAliasRoles.Contains(r.Name) && r.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
        if (legacyDeactivated > 0)
        {
            logger.LogInformation(
                "Aliases legado desactivados (IsActive = false): {Count}",
                legacyDeactivated
            );
        }

        // Marcado IsSystem (idempotente): protege los roles de sistema del
        // renombrado/eliminación accidental, incluso en BD existentes.
        var systemMarked = await dbContext
            .Roles.Where(r => SystemRoleNames.Contains(r.Name) && !r.IsSystem)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsSystem, true), ct);
        if (systemMarked > 0)
        {
            logger.LogInformation("Roles marcados como IsSystem: {Count}", systemMarked);
        }

        // Backfill idempotente de NormalizedName: versiones previas del seeder
        // creaban roles vía DbContext sin normalizar, y sin NormalizedName el
        // lookup de Identity (AddToRoleAsync/FindByNameAsync) falla con
        // "Role X does not exist". Solo completa los que falten.
        var normalizedBackfill = await dbContext
            .Roles.Where(r => r.NormalizedName == null)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(r => r.NormalizedName, r => r.Name!.ToUpper()), ct);
        if (normalizedBackfill > 0)
        {
            logger.LogInformation(
                "Roles con NormalizedName completado: {Count}",
                normalizedBackfill
            );
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
    private static readonly (
        string Role,
        string Description,
        string[] Permissions
    )[] DefaultRoles =
    [
        (
            "OrganizationAdmin",
            "Administra la organización: estructura, empleados y profesionales",
            [
                PermissionCodes.OrganizationsView,
                PermissionCodes.OrganizationsCreate,
                PermissionCodes.OrganizationsUpdate,
                PermissionCodes.OrganizationsDelete,
                PermissionCodes.ClinicsView,
                PermissionCodes.ClinicsCreate,
                PermissionCodes.ClinicsUpdate,
                PermissionCodes.ClinicsDelete,
                PermissionCodes.LocationsView,
                PermissionCodes.LocationsCreate,
                PermissionCodes.LocationsUpdate,
                PermissionCodes.LocationsDelete,
                PermissionCodes.EmployeesView,
                PermissionCodes.EmployeesCreate,
                PermissionCodes.EmployeesUpdate,
                PermissionCodes.EmployeesDelete,
                PermissionCodes.ProfessionalsView,
                PermissionCodes.ProfessionalsCreate,
                PermissionCodes.ProfessionalsUpdate,
                PermissionCodes.ProfessionalsDelete,
                PermissionCodes.PatientsView,
                PermissionCodes.PatientsCreate,
                PermissionCodes.PatientsUpdate,
                PermissionCodes.PatientsDelete,
                PermissionCodes.PatientsExport,
                PermissionCodes.PatientsBulkUpdate,
                PermissionCodes.DocumentsView,
                PermissionCodes.DocumentsUpload,
                PermissionCodes.DocumentsUpdate,
                PermissionCodes.DocumentsDelete,
                PermissionCodes.ClinicalRecordsView,
                PermissionCodes.ClinicalRecordsCreate,
                PermissionCodes.ClinicalRecordsUpdate,
                .. AllTelemedicinePermissions,
                .. ModuleAdminPermissions,
                .. PrescriberPermissions,
                PermissionCodes.CommunityView,
                PermissionCodes.CommunityModerate,
                PermissionCodes.CommunityManage,
                PermissionCodes.LegalDocumentsView,
                PermissionCodes.LegalDocumentsManage,
                PermissionCodes.WellnessView,
                PermissionCodes.WellnessManage,
            ]
        ),
        (
            "ClinicAdmin",
            "Administra una clínica y sus sedes",
            [
                PermissionCodes.ClinicsView,
                PermissionCodes.ClinicsUpdate,
                PermissionCodes.LocationsView,
                PermissionCodes.LocationsCreate,
                PermissionCodes.LocationsUpdate,
                PermissionCodes.EmployeesView,
                PermissionCodes.EmployeesCreate,
                PermissionCodes.EmployeesUpdate,
                PermissionCodes.ProfessionalsView,
                PermissionCodes.ProfessionalsCreate,
                PermissionCodes.ProfessionalsUpdate,
                PermissionCodes.PatientsView,
                PermissionCodes.PatientsCreate,
                PermissionCodes.PatientsUpdate,
                PermissionCodes.DocumentsView,
                PermissionCodes.DocumentsUpload,
                PermissionCodes.DocumentsUpdate,
                PermissionCodes.ClinicalRecordsView,
                PermissionCodes.ClinicalRecordsCreate,
                PermissionCodes.ClinicalRecordsUpdate,
                .. AllTelemedicinePermissions,
                .. ModuleAdminPermissions,
                .. PrescriberPermissions,
                PermissionCodes.CommunityModerate,
                PermissionCodes.CommunityManage,
                PermissionCodes.LegalDocumentsView,
                PermissionCodes.LegalDocumentsManage,
                PermissionCodes.WellnessView,
                PermissionCodes.WellnessManage,
            ]
        ),
        (
            "ClinicalDirector",
            "Dirección clínica: supervisa historiales y profesionales",
            [
                PermissionCodes.PatientsView,
                PermissionCodes.PatientsUpdate,
                PermissionCodes.DocumentsView,
                PermissionCodes.ClinicalRecordsView,
                PermissionCodes.ClinicalRecordsCreate,
                PermissionCodes.ClinicalRecordsUpdate,
                PermissionCodes.ProfessionalsView,
                PermissionCodes.EmployeesView,
                PermissionCodes.TelemedicineSessionsManage,
                .. ProfessionalTelemedicinePermissions,
                .. ModuleAdminPermissions,
                .. PrescriberPermissions,
                PermissionCodes.LegalDocumentsView,
                PermissionCodes.WellnessView,
                PermissionCodes.WellnessManage,
            ]
        ),
        (
            "Professional",
            "Profesional clínico: atiende pacientes, registra historia clínica y agenda citas (consolida los roles legado Physician/Nutritionist/Psychologist)",
            [
                PermissionCodes.PatientsViewOwn,
                PermissionCodes.PatientsCreate,
                PermissionCodes.PatientsUpdate,
                PermissionCodes.DocumentsView,
                PermissionCodes.DocumentsUpload,
                PermissionCodes.DocumentsUpdate,
                PermissionCodes.ClinicalRecordsView,
                PermissionCodes.ClinicalRecordsCreate,
                PermissionCodes.ClinicalRecordsUpdate,
                PermissionCodes.HealthTestsViewOwn,
                PermissionCodes.HealthTestsAssign,
                PermissionCodes.HealthTestsReview,
                .. PrescriberPermissions,
                .. ProfessionalTelemedicinePermissions,
                PermissionCodes.LegalDocumentsView,
            ]
        ),
        (
            "Nurse",
            "Enfermería: soporte clínico y registro",
            [
                PermissionCodes.PatientsViewOwn,
                PermissionCodes.PatientsUpdate,
                PermissionCodes.DocumentsView,
                PermissionCodes.DocumentsUpload,
                PermissionCodes.DocumentsUpdate,
                PermissionCodes.ClinicalRecordsView,
                PermissionCodes.ClinicalRecordsCreate,
                PermissionCodes.ClinicalRecordsUpdate,
                PermissionCodes.PrescriptionsView,
                .. ViewerTelemedicinePermissions,
                PermissionCodes.LegalDocumentsView,
            ]
        ),
        (
            "Receptionist",
            "Recepción: agenda, registro de pacientes y documentos",
            [
                PermissionCodes.PatientsView,
                PermissionCodes.PatientsCreate,
                PermissionCodes.DocumentsView,
                PermissionCodes.DocumentsUpload,
                PermissionCodes.DocumentsUpdate,
                PermissionCodes.ProfessionalsView,
                PermissionCodes.PrescriptionsView,
                .. StaffTelemedicinePermissions,
                .. ModuleAdminPermissions,
                PermissionCodes.LegalDocumentsView,
            ]
        ),
        (
            "CareCoordinator",
            "Coordinación de cuidados: seguimiento del paciente",
            [
                PermissionCodes.PatientsView,
                PermissionCodes.DocumentsView,
                PermissionCodes.ClinicalRecordsView,
                PermissionCodes.ProfessionalsView,
                PermissionCodes.PrescriptionsView,
                .. ViewerTelemedicinePermissions,
                .. ModuleAdminPermissions,
                PermissionCodes.LegalDocumentsView,
                PermissionCodes.WellnessView,
            ]
        ),
        (
            "Coordinator",
            "Coordinador: alias funcional de CareCoordinator (visibilidad y seguimiento)",
            [
                PermissionCodes.PatientsView,
                PermissionCodes.DocumentsView,
                PermissionCodes.ClinicalRecordsView,
                PermissionCodes.ProfessionalsView,
                PermissionCodes.PrescriptionsView,
                .. ViewerTelemedicinePermissions,
                .. ModuleAdminPermissions,
                PermissionCodes.LegalDocumentsView,
                PermissionCodes.WellnessView,
            ]
        ),
        (
            "Finance",
            "Financiero: acceso a información financiera sin acceso clínico",
            [
                PermissionCodes.FinanceView,
                PermissionCodes.FinanceManage,
                PermissionCodes.EmployeesView,
                PermissionCodes.ProfessionalsView,
                PermissionCodes.LegalDocumentsView,
            ]
        ),
        (
            "Auditor",
            "Auditor: solo lectura de reportes, pacientes, documentos y auditoría",
            [
                PermissionCodes.ReportsView,
                PermissionCodes.PatientsView,
                PermissionCodes.DocumentsView,
                PermissionCodes.AuditView,
                PermissionCodes.EmployeesView,
                PermissionCodes.ProfessionalsView,
                PermissionCodes.TelemedicineAdminView,
                PermissionCodes.AppointmentsAdminView,
                PermissionCodes.LegalDocumentsView,
            ]
        ),
    ];
}
