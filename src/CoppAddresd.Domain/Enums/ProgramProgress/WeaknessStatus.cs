namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Ciclo de vida de una debilidad (SPEC §21): <c>open</c> (recién detectada,
/// visible en la cola clínica) → <c>acknowledged</c> (el clínico la reconoció)
/// → <c>in_intervention</c> (hay una intervención en curso) → <c>resolved</c>
/// (cerrada; fija <c>resolved_at</c>) o <c>dismissed</c> (descartada por el
/// clínico). Los nombres del enum son exactamente los valores almacenados
/// (<c>varchar(20)</c> en <c>app.weaknesses.status</c>, default <c>open</c>).
///
/// El motor de detección NUNCA duplica mientras exista una fila
/// <c>open</c>/<c>acknowledged</c>/<c>in_intervention</c> con el mismo
/// <c>code</c> (AC-43); el clínico transiciona con
/// <c>POST /api/v1/program/weaknesses/{{id}}/status</c> (AC-44).
/// </summary>
public enum WeaknessStatus
{
    /// <summary>Recién detectada por el motor (o creada por un profesional).</summary>
    open = 1,

    /// <summary>Reconocida por el clínico; aún sin intervención.</summary>
    acknowledged = 2,

    /// <summary>Hay una intervención (plan, ajuste de reto, telemedicina) en curso.</summary>
    in_intervention = 3,

    /// <summary>Resuelta: la condición que la disparó dejó de cumplirse.</summary>
    resolved = 4,

    /// <summary>Descartada por el clínico (no aplica / falso positivo).</summary>
    dismissed = 5,
}