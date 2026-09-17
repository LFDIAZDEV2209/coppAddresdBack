namespace CoppAddresd.Auth.Configuration;

/// <summary>
/// Configuración del seeder de cuentas demo para pacientes con alertas
/// (<see cref="Seeders.PatientAccountDemoSeeder"/>). Es un no-op salvo que
/// <c>Enabled</c> sea true y haya password: nunca debe activarse en producción.
/// </summary>
public class PatientAccountDemoSettings
{
    public const string SectionName = "PatientAccountDemo";

    /// <summary>Interruptor explícito: sin esto el seeder no toca nada.</summary>
    public bool Enabled { get; set; }

    /// <summary>Password común de todas las cuentas demo.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Máximo de cuentas a crear por corrida (0 = sin límite). Sirve de red de
    /// seguridad en bases con muchos pacientes.
    /// </summary>
    public int MaxAccounts { get; set; }
}
