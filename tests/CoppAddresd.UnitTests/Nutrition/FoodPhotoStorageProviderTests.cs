using Amazon.S3;
using CoppAddresd.Api.Security;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Moq;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class FoodPhotoStorageProviderTests
{
    public static IEnumerable<object[]> Aliases()
    {
        const string key = "foodai/11111111111141118111111111111111.png";
        foreach (var cloud in new[] { false, true })
            foreach (var alias in new[] { key, key.ToUpperInvariant(), "media/../" + key, "./" + key,
                key.Replace('/', '\\'), key.Replace("foodai", "%66oodai"), key.Replace("/", "%252f") })
                yield return [cloud, alias];
    }

    [Theory] [MemberData(nameof(Aliases))]
    public async Task GenericReadersAndSigners_RejectPhotoAliasesBeforeProviderIo(bool cloud, string key)
    {
        var client = new Mock<IAmazonS3>(MockBehavior.Strict);
        var root = Path.Combine(Path.GetTempPath(), "copp-photo-denied-" + Guid.NewGuid().ToString("N"));
        IObjectStorageService service = cloud ? new S3ObjectStorageService(client.Object, "private-qa-bucket")
            : new LocalObjectStorageService(Options.Create(new LocalStorageOptions { RootPath = root }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetObjectAsync(key));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetPreSignedUrlAsync(key, TimeSpan.FromMinutes(5)));
        var signatures = new StorageSignatureService(Guid.NewGuid().ToString());
        Assert.Throws<UnauthorizedAccessException>(() => signatures.Sign(key, DateTimeOffset.UtcNow.AddMinutes(5)));
        client.VerifyNoOtherCalls(); Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void ExplicitPhotoSignature_ValidatesOnlyTheExactKeyAndShortExpiry()
    {
        const string key = "foodai/11111111111141118111111111111111.png";
        var service = new StorageSignatureService(Guid.NewGuid().ToString());
        var now = DateTimeOffset.UtcNow; var expires = now.AddMinutes(5);
        var signature = service.SignFoodPhoto(key, expires);
        Assert.True(service.Validate(key, signature, expires.ToUnixTimeSeconds().ToString(), now));
        Assert.False(service.Validate(key, signature, expires.ToUnixTimeSeconds().ToString(), expires.AddSeconds(1)));
        Assert.False(service.Validate(key.Replace("1111.png", "2222.png"), signature, expires.ToUnixTimeSeconds().ToString(), now));
    }
}
