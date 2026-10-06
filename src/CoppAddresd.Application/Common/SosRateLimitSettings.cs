namespace CoppAddresd.Application.Common;

/// <summary>
/// Interruptor del rate-limit distribuido de SOS (REQ-SOS-02).
/// Config: "Sos": { "RateLimit": { "Enabled": true } }.
/// Por defecto DESACTIVADO a pedido de producto (2026-10-06): el botón de
/// pánico nunca debe rebotar por cuota mientras no se pida explícitamente.
/// Al reactivarlo aplican cooldown 60 s, 3/15 min, 10/día y lockout 15 min.
/// </summary>
public sealed class SosRateLimitSettings
{
    public const string SectionName = "Sos:RateLimit";

    /// <summary>
    /// true = aplicar cuotas (Valkey); false (default) = permitir siempre y no
    /// registrar consumo. El hard-guarantee de una sola alerta activa por
    /// paciente vive en el índice parcial de PostgreSQL, independiente de esto.
    /// </summary>
    public bool Enabled { get; set; }
}
