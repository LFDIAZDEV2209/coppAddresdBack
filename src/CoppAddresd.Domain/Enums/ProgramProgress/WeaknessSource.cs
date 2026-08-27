namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Origen de una debilidad (SPEC §21): quién la registró. Los nombres del enum
/// son exactamente los valores almacenados (<c>varchar(20)</c> en
/// <c>app.weaknesses.source</c>, default <c>ai</c>): <c>ai</c> (detectada por el
/// motor determinista en <c>POST /scores/calculate</c>), <c>professional</c>
/// (registrada por un clínico) y <c>system</c> (proceso automático de
/// infraestructura). Si un clínico valida después una fila <c>ai</c>, el origen
/// permanece <c>ai</c> (la transición de estado no reescribe la procedencia).
/// </summary>
public enum WeaknessSource
{
    /// <summary>Detectada por el motor determinista de reglas (SPEC §21, B).</summary>
    ai = 1,

    /// <summary>Registrada por un profesional durante su revisión.</summary>
    professional = 2,

    /// <summary>Generada por un proceso del sistema.</summary>
    system = 3,
}