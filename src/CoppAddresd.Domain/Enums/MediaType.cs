namespace CoppAddresd.Domain.Enums;

/// <summary>
/// Tipo de medio audiovisual servido a los pacientes. Extensible para cubrir
/// nuevos formatos sin cambiar el modelo de persistencia.
/// </summary>
public enum MediaType
{
    Podcast = 1,
    Video = 2,
    Audio = 3,
}
