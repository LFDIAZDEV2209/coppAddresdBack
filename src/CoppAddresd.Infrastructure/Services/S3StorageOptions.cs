namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Opciones del almacenamiento de objetos AWS S3. Sección de configuración:
/// <c>Storage:S3</c>. Cada propiedad cae a la variable de entorno AWS estándar
/// correspondiente cuando no está configurada en <c>appsettings</c> (así la
/// misma imagen se despliega en EC2/ECS sin reconfigurar).
/// </summary>
/// <remarks>
/// El SDK resuelve las credenciales por la cadena por defecto (IAM role de la
/// instancia/tarea en producción; <c>AWS_PROFILE</c> o variables en local).
/// Nunca se configuran Access Keys aquí ni en código.
/// </remarks>
public class S3StorageOptions
{
    public const string SectionName = "Storage:S3";

    /// <summary>Región del bucket (fallback: <c>AWS_REGION</c>).</summary>
    public string? Region { get; set; }

    /// <summary>Nombre del bucket (fallback: <c>AWS_S3_BUCKET</c>).</summary>
    public string? Bucket { get; set; }

    /// <summary>ARN del bucket (referencia/documentación; fallback: <c>AWS_S3_BUCKET_ARN</c>).</summary>
    public string? BucketArn { get; set; }

    /// <summary>ARN de los objetos del bucket (políticas; fallback: <c>AWS_S3_OBJECT_ARN</c>).</summary>
    public string? ObjectArn { get; set; }

    /// <summary>Nombre del IAM role con acceso al bucket (fallback: <c>AWS_S3_IAM_ROLE</c>).</summary>
    public string? IamRole { get; set; }

    /// <summary>
    /// URL de servicio S3 compatible (LocalStack/MinIO) para desarrollo.
    /// Vacío en producción (se usa el endpoint real de AWS).
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>Aplica los valores por defecto desde variables de entorno estándar de AWS.</summary>
    public void ApplyEnvironmentDefaults()
    {
        Region ??= System.Environment.GetEnvironmentVariable("AWS_REGION");
        Bucket ??= System.Environment.GetEnvironmentVariable("AWS_S3_BUCKET");
        BucketArn ??= System.Environment.GetEnvironmentVariable("AWS_S3_BUCKET_ARN");
        ObjectArn ??= System.Environment.GetEnvironmentVariable("AWS_S3_OBJECT_ARN");
        IamRole ??= System.Environment.GetEnvironmentVariable("AWS_S3_IAM_ROLE");
        ServiceUrl ??= System.Environment.GetEnvironmentVariable("AWS_S3_ENDPOINT");
    }
}