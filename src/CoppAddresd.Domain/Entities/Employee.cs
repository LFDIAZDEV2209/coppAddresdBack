namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Empleado del ERP. Se relaciona 1:1 con <c>auth.users</c> (sin FK en el
/// modelo EF: el usuario vive en el esquema auth, fuera de este DbContext;
/// la restricción se crea por SQL en la migración). La identidad básica
/// (nombres, email) reside en auth.users para no duplicarla.
/// </summary>
public sealed class Employee
{
    public Guid Id { get; set; }

    /// <summary>Id del usuario en <c>auth.users</c>.</summary>
    public Guid UserId { get; set; }

    public string? JobTitle { get; set; }

    public string? Department { get; set; }

    public DateTime? HireDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}