namespace CoppAddresd.Domain.Enums;

/// <summary>
/// Severidad de una regla de seguridad clínica para la generación de planes:
/// una violación <c>Block</c> impide prescribir el contenido (condiciona el
/// plan de forma excluyente), mientras que <c>Warning</c> solo lo condiciona.
/// </summary>
public enum SafetySeverity
{
    Block = 1,
    Warning = 2,
}