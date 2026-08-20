namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Asignación N:N de un <see cref="Employee"/> a una <see cref="Clinic"/>.
/// Vive a nivel empleado (no solo profesional): recepción, finanzas y
/// administración también trabajan en clínicas. Es la base del modelo de
/// permisos por contexto (Fase 2) y del switcher de contexto: los roles y
/// permisos con scope de clínica pueden diferir entre clínicas del mismo
/// usuario. <c>IsPrimary</c> marca la clínica principal.
/// </summary>
public sealed class EmployeeClinic
{
    public Guid EmployeeId { get; set; }

    public Guid ClinicId { get; set; }

    public bool IsPrimary { get; set; }

    /// <summary>Estado de la asignación (Active, Inactive).</summary>
    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Employee Employee { get; set; } = default!;

    public Clinic Clinic { get; set; } = default!;
}
