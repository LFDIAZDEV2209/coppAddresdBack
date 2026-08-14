namespace CoppAddresd.Domain.Enums;

/// <summary>
/// Categoría temática de una lección. Agrupa el contenido audiovisual para que
/// el paciente explore temas específicos y el equipo clínico lo ordene.
/// Extensible para cubrir nuevas áreas del programa sin cambiar el modelo de
/// persistencia.
/// </summary>
public enum MediaCategory
{
    Biologia = 1,
    Nutricion = 2,
    Psicologia = 3,
    CrecimientoPersonal = 4,
    Habitos = 5,
    SaludFisica = 6,
    BienestarEmocional = 7,
    Mindfulness = 8,
    Motivacion = 9,
}
