namespace CoppAddresd.Auth.Entities;

/// <summary>
/// Código OTP emitido para verificar la identidad de un paciente durante el
/// primer inicio de sesión (login por número de identificación). Almacenado
/// como hash (SHA-256 con salt) — nunca en claro — y con expiración corta.
/// </summary>
public class OtpCode
{
    public Guid Id { get; set; }

    /// <summary>
    /// Número de identificación del paciente al que se emitió el código.
    /// Es el vínculo con <c>app.patient_profiles.document_number</c>.
    /// </summary>
    public string DocumentNumber { get; set; } = string.Empty;

    /// <summary>
    /// Canal elegido por el usuario: <c>"Email"</c> o <c>"Phone"</c>.
    /// </summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Destino real (correo o teléfono con código de país) al que se envió.
    /// Se resuelve en el servidor desde el contacto elegido; el cliente nunca
    /// envía el destino en claro.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Hash SHA-256 del código: <c>SHA256(salt + code)</c>. Nunca se persiste
    /// el código en claro.
    /// </summary>
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>
    /// Salt aleatorio usado al calcular <see cref="CodeHash"/>.
    /// </summary>
    public string Salt { get; set; } = string.Empty;

    /// <summary>
    /// Intentos fallidos de verificación. Límite (5) para mitigar fuerza bruta.
    /// </summary>
    public int Attempts { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsUsed => UsedAt.HasValue;
}
