namespace CoppAddresd.Domain.Entities.FoodAi;

/// <summary>
/// Alias de un alimento: nombre tal como lo devuelve el modelo de visión
/// (ej. "banana") mapeado al alimento canónico de la fuente nutricional
/// (ej. "Banana, raw"). Evita acoplar el nombre del modelo a la BD.
/// </summary>
public sealed class FoodAlias
{
    public Guid Id { get; set; }
    public Guid FoodId { get; set; }
    public string Alias { get; set; } = default!;
    public string Source { get; set; } = default!;

    public Food Food { get; set; } = default!;
}