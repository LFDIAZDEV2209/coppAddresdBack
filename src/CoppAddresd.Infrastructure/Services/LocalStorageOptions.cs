namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Opciones del almacenamiento de objetos local (sistema de archivos).
/// Sección de configuración: <c>Storage:Local</c>.
/// </summary>
public class LocalStorageOptions
{
    public const string SectionName = "Storage:Local";

    /// <summary>
    /// Directorio raíz donde se persisten los objetos. Si no se configura,
    /// se usa un directorio temporal del sistema dedicado a CoppAddresd.
    /// </summary>
    public string RootPath { get; set; } = Path.Combine(Path.GetTempPath(), "coppaddresd-storage");
}
