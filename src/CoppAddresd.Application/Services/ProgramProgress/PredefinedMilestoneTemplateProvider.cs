using CoppAddresd.Application.Common;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Proveedor v1 de plantillas estáticas: construye en memoria el diccionario
/// día → plantilla desde <c>Program:MilestoneSender:Templates</c> (copy
/// neutral en español definido por producto). Cuando el contenido se genere
/// con LLM, el nuevo proveedor implementará
/// <see cref="IProgramMilestoneTemplateProvider"/> sin cambios en el job.
/// </summary>
public sealed class PredefinedMilestoneTemplateProvider : IProgramMilestoneTemplateProvider
{
    private readonly IReadOnlyDictionary<int, MilestoneTemplate> _templates;

    public PredefinedMilestoneTemplateProvider(IOptions<ProgramMilestoneSenderSettings> options)
    {
        _templates = options.Value.Templates
            .Where(t => t.Day > 0)
            .GroupBy(t => t.Day)
            .ToDictionary(g => g.Key, g => ToTemplate(g.Last()));
    }

    /// <summary>
    /// Plantilla del día de hito o null si el día no está configurado.
    /// </summary>
    public Task<MilestoneTemplate?> GetAsync(int milestoneDay, CancellationToken ct = default)
        => Task.FromResult(
            _templates.TryGetValue(milestoneDay, out var template) ? template : null);

    private static MilestoneTemplate ToTemplate(MilestoneTemplateSettings settings)
        => new(
            settings.Title,
            settings.Message,
            string.IsNullOrWhiteSpace(settings.AgentTypeId) ? "base" : settings.AgentTypeId);
}
