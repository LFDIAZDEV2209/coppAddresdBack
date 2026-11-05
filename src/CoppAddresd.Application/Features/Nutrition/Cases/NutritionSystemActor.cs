namespace CoppAddresd.Application.Features.Nutrition.Cases;

/// <summary>Identidad técnica reservada, nunca un usuario autenticable ni un profesional.</summary>
public static class NutritionSystemActor
{
    public const string Name = "system:nutrition";
    public static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000061");
    public static NutritionActor Actor => new(Id, null, true, true, false);
    private static readonly AsyncLocal<bool> Active = new();
    public static bool IsActive => Active.Value;
    public static IDisposable Begin()
    {
        var previous = Active.Value;
        Active.Value = true;
        return new Scope(previous);
    }
    private sealed class Scope(bool previous) : IDisposable
    {
        public void Dispose() => Active.Value = previous;
    }
}
