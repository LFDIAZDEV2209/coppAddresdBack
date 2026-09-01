namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Ítem de una batería: referencia el instrumento y (opcionalmente) una versión
/// concreta. Si <c>VersionId</c> es nulo, la asignación usa la versión activa
/// del instrumento en el momento de asignar. Define orden, obligatoriedad y
/// periodicidad en días (SPEC A4).
/// </summary>
public sealed class HealthTestBatteryItem
{
    public Guid Id { get; set; }

    public Guid BatteryId { get; set; }

    public Guid InstrumentId { get; set; }

    /// <summary>Versión fijada; null = versión activa al asignar.</summary>
    public Guid? VersionId { get; set; }

    public int SortOrder { get; set; }

    public bool IsRequired { get; set; } = true;

    /// <summary>Periodicidad en días para re-asignación futura (null = única).</summary>
    public int? FrequencyDays { get; set; }

    // Navigation
    public HealthTestBattery? Battery { get; set; }

    public HealthTestInstrument? Instrument { get; set; }

    public HealthTestVersion? Version { get; set; }
}
