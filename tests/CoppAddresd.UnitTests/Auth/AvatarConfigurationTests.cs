using System.Security.Claims;
using System.Text.Json;
using CoppAddresd.Auth.Avatar.Application;
using CoppAddresd.Auth.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.UnitTests.Auth;

public class AvatarConfigurationTests
{
    private sealed class Store : IAvatarPreferenceStore
    {
        public Dictionary<Guid, string> Values { get; } = [];
        public Task<string?> ReadAsync(Guid id, CancellationToken ct) => Task.FromResult(Values.GetValueOrDefault(id));
        public Task<string?> ProfileGenderAsync(Guid id, CancellationToken ct) => Task.FromResult<string?>("Femenino");
        public Task WriteAsync(Guid id, string value, CancellationToken ct) { ct.ThrowIfCancellationRequested(); Values[id] = value; return Task.CompletedTask; }
    }
    private static AvatarConfigurationController Controller(Store store, Guid? id) => new(new(store))
    {
        ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(
            new ClaimsIdentity(id is null ? [] : [new Claim(ClaimTypes.NameIdentifier, id.Value.ToString())], "test")) } }
    };

    [Fact]
    public void LegacyConfiguration_GetsDefaultSkin_AndInvalidSkinIsRejected()
    {
        var json="""{"version":1,"gender":"female","hair":"hair-02","clothing":{"shirt":"shirt-basic-01","pants":null,"shoes":null},"accessories":{"glasses":null,"watch":null,"bracelet":null}}""";
        var old=JsonSerializer.Deserialize<AvatarConfiguration>(json,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(old);Assert.Equal("skin-03",old.Skin);Assert.True(old.IsValid());
        Assert.False((old with {Skin="skin-99"}).IsValid());
    }
    [Fact]
    public async Task Defaults_UseProfile_WithoutWritingClinicalOrPreferenceData()
    {
        var store = new Store(); var result = await new AvatarConfigurationUseCases(store).GetAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal("female", result.Gender); Assert.Equal("hair-02", result.Hair); Assert.True(result.IsValid()); Assert.Empty(store.Values);
    }
    [Fact]
    public async Task Controller_UsesClaim_AndIsolatesUsers()
    {
        var store = new Store(); var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var selection = AvatarConfiguration.Default("male") with { Hair = "hair-03", Accessories = new("glasses-01", "watch-01", "bracelet-01") };
        Assert.IsType<OkObjectResult>((await Controller(store,a).Put(selection,CancellationToken.None)).Result);
        Assert.Equal(selection, Assert.IsType<AvatarConfiguration>(Assert.IsType<OkObjectResult>((await Controller(store,a).Get(CancellationToken.None)).Result).Value));
        Assert.Equal("female", Assert.IsType<AvatarConfiguration>(Assert.IsType<OkObjectResult>((await Controller(store,b).Get(CancellationToken.None)).Result).Value).Gender);
        Assert.False(store.Values.ContainsKey(b));
        Assert.IsType<UnauthorizedResult>((await Controller(store,null).Get(CancellationToken.None)).Result);
        Assert.IsType<UnauthorizedResult>((await Controller(store,null).Put(selection,CancellationToken.None)).Result);
        Assert.NotEmpty(typeof(AvatarConfigurationController).GetCustomAttributes(typeof(AuthorizeAttribute),true));
    }
    [Theory]
    [InlineData("female", "hair-03")]
    [InlineData("male", "../body.glb")]
    [InlineData("other", "hair-02")]
    public async Task InvalidGenderOrAsset_IsRejected(string gender, string hair)
    {
        var store = new Store();
        Assert.False(await new AvatarConfigurationUseCases(store).PutAsync(Guid.NewGuid(),AvatarConfiguration.Default("male") with { Gender=gender, Hair=hair },CancellationToken.None));
        Assert.Empty(store.Values);
    }
    [Fact]
    public void ClinicalFieldsAndUserId_AreRejectedByJsonContract()
    {
        foreach (var field in new[] { "userId", "body", "weight", "morphs" })
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AvatarConfiguration>("{\""+field+"\":1}",new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.False((AvatarConfiguration.Default("male") with { Clothing=new("hair-02",null,null) }).IsValid());
        Assert.False((AvatarConfiguration.Default("male") with { Clothing=new(null,"pants-missing",null) }).IsValid());
        Assert.False((AvatarConfiguration.Default("male") with { Version=2 }).IsValid());
    }
    [Fact]
    public async Task CancelledWrite_DoesNotPersist()
    {
        var store=new Store(); using var cts=new CancellationTokenSource();cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(()=>new AvatarConfigurationUseCases(store).PutAsync(Guid.NewGuid(),AvatarConfiguration.Default(null),cts.Token));
        Assert.Empty(store.Values);
    }

    [Theory]
    [InlineData("female-hair-long-01")]
    [InlineData("female-hair-long-02")]
    [InlineData("female-hair-long-03")]
    public async Task ActiveLongHair_RoundTrips_OnlyForCompatibleGender(string hair)
    {
        var store = new Store(); var user = Guid.NewGuid(); var other = Guid.NewGuid();
        var useCases = new AvatarConfigurationUseCases(store);
        var selected = AvatarConfiguration.Default("female") with { Hair = hair,
            Accessories = new("glasses-01", "watch-01", "bracelet-01") };
        Assert.True(await useCases.PutAsync(user, selected, CancellationToken.None));
        Assert.Equal(selected, await useCases.GetAsync(user, CancellationToken.None));
        Assert.False(await useCases.PutAsync(other, selected with { Gender = "male" }, CancellationToken.None));
        Assert.False(await useCases.PutAsync(other, selected with { Clothing = new(hair, null, null) }, CancellationToken.None));
        Assert.False(store.Values.ContainsKey(other));
    }
}
