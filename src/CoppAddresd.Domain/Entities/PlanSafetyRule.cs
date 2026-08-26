using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Regla de seguridad clínica que condiciona la generación de planes de
/// alimentación y rutinas de ejercicio con IA. Se evalúa contra el valor
/// consolidado de una métrica del paciente (por <see cref="MetricCode"/>);
/// cuando la condición se cumple, la <see cref="Restriction"/> (texto legible
/// para la IA) se envía al AI Service para condicionar el plan generado.
/// Los umbrales de referencia se basan en guías ADA/OMS conservadoras y deben
/// validarse clínicamente. Las unidades se comparan por código
/// (<see cref="UnitCode"/>, ej. <c>mg_dl</c>).
/// </summary>
public sealed class PlanSafetyRule
{
    public Guid Id { get; set; }

    /// <summary>Código de la métrica evaluada (ej. <c>glucose_fasting</c>).</summary>
    public string MetricCode { get; set; } = default!;

    /// <summary>Operador de comparación: <c>&gt;</c>, <c>&gt;=</c>, <c>&lt;</c>, <c>&lt;=</c> o <c>between</c>.</summary>
    public string Operator { get; set; } = default!;

    /// <summary>Umbral inferior (para <c>between</c> es el mínimo).</summary>
    public decimal? ThresholdMin { get; set; }

    /// <summary>Umbral superior (solo para <c>between</c>).</summary>
    public decimal? ThresholdMax { get; set; }

    /// <summary>Código de la unidad en la que se expresan los umbrales (ej. <c>mg_dl</c>).</summary>
    public string UnitCode { get; set; } = default!;

    /// <summary>Texto legible de la restricción que se envía a la IA.</summary>
    public string Restriction { get; set; } = default!;

    /// <summary>Severidad de la restricción: <c>block</c> | <c>warning</c>.</summary>
    public SafetySeverity Severity { get; set; } = SafetySeverity.Warning;

    /// <summary>Indica si la regla está vigente para nuevas generaciones.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Orden de evaluación (las restricciones más restrictivas primero).</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}