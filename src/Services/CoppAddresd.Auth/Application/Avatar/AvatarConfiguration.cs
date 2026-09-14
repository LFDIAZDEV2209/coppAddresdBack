using System.Text.Json.Serialization;
using CoppAddresd.Auth.Domain.Avatar;

namespace CoppAddresd.Auth.Avatar.Application;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AvatarClothing(string? Shirt, string? Pants, string? Shoes);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AvatarAccessories(string? Glasses, string? Watch, string? Bracelet);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AvatarConfiguration(int Version, string Gender, string? Hair,
    AvatarClothing Clothing, AvatarAccessories Accessories)
{
    public bool IsValid() => Version == 1 && Gender is "male" or "female"
        && Clothing is not null && Accessories is not null
        && AvatarCatalog.Accepts(Gender, "hair", Hair)
        && AvatarCatalog.Accepts(Gender, "shirt", Clothing.Shirt)
        && AvatarCatalog.Accepts(Gender, "pants", Clothing.Pants)
        && AvatarCatalog.Accepts(Gender, "shoes", Clothing.Shoes)
        && AvatarCatalog.Accepts(Gender, "glasses", Accessories.Glasses)
        && AvatarCatalog.Accepts(Gender, "watch", Accessories.Watch)
        && AvatarCatalog.Accepts(Gender, "bracelet", Accessories.Bracelet);

    public static AvatarConfiguration Default(string? profileGender) => new(1,
        AvatarCatalog.DefaultGender(profileGender), "hair-02", new("shirt-basic-01", null, null), new(null, null, null));
}
