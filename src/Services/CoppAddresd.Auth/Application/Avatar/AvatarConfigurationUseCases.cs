using System.Text.Json;

namespace CoppAddresd.Auth.Avatar.Application;

public interface IAvatarPreferenceStore
{
    Task<string?> ReadAsync(Guid userId, CancellationToken ct);
    Task<string?> ProfileGenderAsync(Guid userId, CancellationToken ct);
    Task WriteAsync(Guid userId, string configuration, CancellationToken ct);
}

public sealed class AvatarConfigurationUseCases(IAvatarPreferenceStore store)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<AvatarConfiguration> GetAsync(Guid userId, CancellationToken ct)
    {
        var json = await store.ReadAsync(userId, ct);
        if (json is null) return AvatarConfiguration.Default(await store.ProfileGenderAsync(userId, ct));
        var value = JsonSerializer.Deserialize<AvatarConfiguration>(json, Json);
        if (value is null || !value.IsValid()) throw new InvalidOperationException("Configuración de avatar incompatible.");
        return value;
    }
    public async Task<bool> PutAsync(Guid userId, AvatarConfiguration value, CancellationToken ct)
    {
        if (!value.IsValid()) return false;
        await store.WriteAsync(userId, JsonSerializer.Serialize(value, Json), ct);
        return true;
    }
}
