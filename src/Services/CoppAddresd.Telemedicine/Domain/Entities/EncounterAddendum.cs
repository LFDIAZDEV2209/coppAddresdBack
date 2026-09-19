namespace CoppAddresd.Telemedicine.Domain.Entities;

/// <summary>
/// Adenda de un encuentro clínico completado (F4): corrección o aclaración
/// posterior al cierre del registro. Es append-only (sin edición ni borrado) y
/// no modifica el encuentro original, que sigue inmutable. Guarda el autor por
/// identidad (<see cref="AuthorUserId"/>) y un snapshot legible de su nombre
/// (<see cref="AuthorName"/>) para trazabilidad legal a largo plazo.
/// </summary>
public sealed class EncounterAddendum
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Encuentro clínico al que se agrega la adenda.</summary>
    public Guid EncounterId { get; set; }

    /// <summary>Usuario de <c>auth.users</c> autor de la adenda (identidad del JWT).</summary>
    public Guid AuthorUserId { get; set; }

    /// <summary>Snapshot del nombre del autor al momento de firmar (máx. 200, nullable).</summary>
    public string? AuthorName { get; set; }

    /// <summary>Texto de la adenda (obligatorio, 1–2000 caracteres).</summary>
    public string Body { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ClinicalEncounter? Encounter { get; set; }
}
