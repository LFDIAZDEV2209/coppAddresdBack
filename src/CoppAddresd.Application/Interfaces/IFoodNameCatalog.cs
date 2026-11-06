namespace CoppAddresd.Application.Interfaces;

/// <summary>Nombres ingleses de identidades exactas en el catálogo curado; no mapea equivalencias nutricionales.</summary>
public interface IFoodNameCatalog
{
    Task<IReadOnlyDictionary<string, string>> GetEnglishNamesAsync(
        IReadOnlyCollection<string> sourceNames, CancellationToken ct);
}
