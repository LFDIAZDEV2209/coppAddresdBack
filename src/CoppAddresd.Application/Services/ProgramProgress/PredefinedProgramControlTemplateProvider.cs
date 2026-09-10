using CoppAddresd.Application.Common;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Proveedor v1 de plantillas estáticas: construye en memoria el diccionario
/// día → plantilla desde <c>Program:Controls:Templates</c> (copy neutral en
/// español definido por producto). Cuando el contenido se genere con LLM, el
/// nuevo proveedor implementará
/// <see cref="IProgramControlTemplateProvider"/> sin cambios en el job.
/// </summary>
public sealed class PredefinedProgramControlTemplateProvider : IProgramControlTemplateProvider
{
    private readonly IReadOnlyDictionary<int, ProgramControlTemplate> _templates;

    public PredefinedProgramControlTemplateProvider(IOptions<ProgramControlSettings> options)
    {
        _templates = options.Value.Templates
            .Where(t => t.Day > 0)
            .GroupBy(t => t.Day)
            .ToDictionary(g => g.Key, g => ToTemplate(g.Last()));
    }

    /// <summary>
    /// Plantilla del día de hito o null si el día no está configurado.
    /// </summary>
    public Task<ProgramControlTemplate?> GetAsync(int milestoneDay, CancellationToken ct = default)
        => Task.FromResult(
            _templates.TryGetValue(milestoneDay, out var template) ? template : null);

    private static ProgramControlTemplate ToTemplate(ProgramControlTemplateSettings settings)
        => new(
            settings.Title,
            settings.Message,
            string.IsNullOrWhiteSpace(settings.AgentTypeId) ? "base" : settings.AgentTypeId);
}