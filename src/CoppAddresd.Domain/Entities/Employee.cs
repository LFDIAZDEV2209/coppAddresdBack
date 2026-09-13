namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Empleado del ERP: entidad núcleo de RRHH para TODA persona que trabaja en
/// la organización (clínicos, recepción, finanzas, administración). El vínculo
/// 1:1 con <c>auth.users</c> es nullable hasta que la invitación crea la
/// cuenta. Los profesionales clínicos agregan su extensión en
/// <see cref="Professional"/> (relación 1:0..1 vía <c>employee_id</c>): así el
/// "ser profesional" es un rol de datos extensible y no obliga a que todo
/// empleado cargue columnas clínicas sin sentido.
/// </summary>
public sealed class Employee
{
    public Guid Id { get; set; }

    /// <summary>Id del usuario en <c>auth.users</c>. Null hasta la invitación.</summary>
    public Guid? UserId { get; set; }

    public Guid OrganizationId { get; set; }

    public string FirstName { get; set; } = default!;

    public string? MiddleName { get; set; }

    public string LastName { get; set; } = default!;

    /// <summary>Email laboral: identidad de la invitación (único por organización).</summary>
    public string Email { get; set; } = default!;

    public string? PhoneCountryCode { get; set; }

    public string? PhoneNumber { get; set; }

    /// <summary>Título del puesto para personal no clínico (ej. "Office Manager"). Null para clínicos: su profesión vive en <see cref="Professional.ProfessionalTypeId"/>.</summary>
    public string? JobTitle { get; set; }

    /// <summary>Departamento (ej. "Finance", "Operations"). Texto libre en Fase 1; candidato a catálogo si el cliente lo exige.</summary>
    public string? Department { get; set; }

    public DateOnly? HireDate { get; set; }

    /// <summary>Estado del ciclo de vida (Invited, Active, Inactive).</summary>
    public string Status { get; set; } = "Invited";

    /// <summary>Última versión de acceso ERP proyectada desde Auth.</summary>
    public long ErpAccessVersion { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Organization Organization { get; set; } = default!;

    public Professional? Professional { get; set; }

    public ICollection<EmployeeClinic> ClinicAssignments { get; set; } = [];
}
