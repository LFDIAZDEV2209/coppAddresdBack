namespace CoppAddresd.Application.Common;

/// <summary>Configuración del flujo de asignación posterior a la batería inicial.</summary>
public sealed class ProgramAssignmentSettings
{
    public const string SectionName = "Program:Assignment";

    /// <summary>Desactivado por defecto; el host habilita Development antes de enlazar configuración.</summary>
    public bool Enabled { get; set; }

    /// <summary>Cortes operativos configurables del porcentaje de capacidad de Movimiento.</summary>
    public decimal MovementLowMaxPercentage { get; set; } = 100m / 3m;
    public decimal MovementMediumMaxPercentage { get; set; } = 200m / 3m;

    /// <summary>Capacidad acotada del canal de narrativas; la propuesta se conserva si se llena.</summary>
    public int NarrativeQueueCapacity { get; set; } = 100;
    public int NarrativeMaxAttempts { get; set; } = 3;
    /// <summary>Antigüedad mínima y pausa tras un intento antes de recuperar narrativas por lote.</summary>
    public int NarrativeDebounceSeconds { get; set; } = 60;
    public int JobTickMinutes { get; set; } = 5;
    public int JobBatchSize { get; set; } = 100;
    public int RecoveryMaxAttempts { get; set; } = 5;
    public int RecoveryBaseDelaySeconds { get; set; } = 60;
    /// <summary>Idioma de entrega cuando aún no existe una preferencia de idioma por usuario.</summary>
    public string NotificationLanguage { get; set; } = "es";
    public string NotificationAgentTypeId { get; set; } = "base";
}
