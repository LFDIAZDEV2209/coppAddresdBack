using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Moq;

namespace CoppAddresd.UnitTests.Documents;

public sealed class DocumentArchiveStorageProviderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "copp-archive-guard-" + Guid.NewGuid().ToString("N"));
    private const string Key = "documents/11111111-1111-4111-8111-111111111111/22222222-2222-4222-8222-222222222222/original.pdf";
    private const string PublicKey = "documents/11111111-1111-4111-8111-111111111111/legacy.pdf";
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    private IObjectStorageService Service(bool cloud, Mock<IAmazonS3> client) => cloud
        ? new S3ObjectStorageService(client.Object, "synthetic-private-bucket")
        : new LocalObjectStorageService(Options.Create(new LocalStorageOptions { RootPath = root }));
    public static TheoryData<bool, string> ProtectedAliases
    {
        get {
            var data = new TheoryData<bool, string>();
            foreach (var cloud in new[] { false, true })
                foreach (var key in new[] { Key, Key.Replace('/', '\\'), "outside/../" + Key, "./" + Key, "/" + Key,
                    Key.Replace("documents/", "documents//"), Key.Replace("documents", "DOCUMENTS"),
                    Key.Replace("documents", "%64ocuments"), Key.Replace("/", "%2F"), Key.Replace("/", "%252f"),
                    Key.Replace("/", "%5c"), Key.Replace("/original.pdf", "/extraction.json") }) data.Add(cloud, key);
            return data;
        }
    }

    [Theory, MemberData(nameof(ProtectedAliases))]
    public async Task GenericOperations_RejectArchiveAndAliasesBeforeAnyProviderIo(bool cloud, string key)
    {
        var client = new Mock<IAmazonS3>(MockBehavior.Strict); var service = Service(cloud, client);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetPreSignedUrlAsync(key, TimeSpan.FromMinutes(15)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetPreSignedUploadUrlAsync(key, "application/pdf", TimeSpan.FromMinutes(15), "https://qa.invalid"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetObjectAsync(key));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.HeadObjectAsync(key));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.PutObjectAsync(key, new MemoryStream([1])));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteObjectAsync(key));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteObjectsAsync(new[] { PublicKey, key }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CopyObjectAsync(key, PublicKey));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CopyObjectAsync(PublicKey, key));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ListObjectsAsync(key));
        client.VerifyNoOtherCalls(); Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task ExplicitLocalArchiveFlow_RoundTripsBytes_HidesArchiveFromGenericListing_AndRejectsNoncanonicalKeys()
    {
        var service = Service(false, new Mock<IAmazonS3>(MockBehavior.Strict));
        await service.PutDocumentArchiveObjectAsync(Key, new MemoryStream(Encoding.UTF8.GetBytes("original byte content")), "application/pdf");
        await using (var stream = await service.GetDocumentArchiveObjectAsync(Key)) {
            using var reader = new StreamReader(stream); Assert.Equal("original byte content", await reader.ReadToEndAsync());
        }
        await service.PutObjectAsync(PublicKey, new MemoryStream([1]));
        var generic = await service.ListObjectsAsync(""); Assert.Equal(PublicKey, Assert.Single(generic.Items).Key);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetDocumentArchiveObjectAsync(Key.Replace("/", "%2f")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetDocumentArchiveReadUrlAsync(PublicKey, TimeSpan.FromMinutes(15)));
        await service.DeleteDocumentArchiveObjectAsync(Key);
        await Assert.ThrowsAsync<FileNotFoundException>(() => service.GetDocumentArchiveObjectAsync(Key));
        // Un lote mixto falla antes de borrar incluso la clave general.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteObjectsAsync(new[] { PublicKey, Key }));
        await using var remaining = await service.GetObjectAsync(PublicKey); Assert.Equal(1, remaining.Length);
    }

    [Fact]
    public async Task ExplicitS3ArchiveFlow_UsesExactKeysAndGetOnlyPresign_AndGenericListingOmitsArchives()
    {
        var client = new Mock<IAmazonS3>(MockBehavior.Strict);
        client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PutObjectResponse { ETag = "synthetic" });
        client.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GetObjectResponse { ResponseStream = new MemoryStream([1, 2, 3]) });
        client.Setup(x => x.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>())).Returns("https://synthetic.invalid/get-capability");
        client.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new DeleteObjectResponse());
        client.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ListObjectsV2Response { S3Objects = [new S3Object { Key = Key }, new S3Object { Key = PublicKey }] });
        var service = Service(true, client);
        await service.PutDocumentArchiveObjectAsync(Key, new MemoryStream([1, 2, 3]), "application/pdf");
        await using var stream = await service.GetDocumentArchiveObjectAsync(Key); Assert.Equal(3, stream.Length);
        Assert.Equal("https://synthetic.invalid/get-capability", await service.GetDocumentArchiveReadUrlAsync(Key, TimeSpan.FromMinutes(15)));
        await service.DeleteDocumentArchiveObjectAsync(Key);
        Assert.Equal(PublicKey, Assert.Single((await service.ListObjectsAsync("")).Items).Key);
        client.Verify(x => x.GetPreSignedURL(It.Is<GetPreSignedUrlRequest>(r => r.Key == Key && r.Verb == HttpVerb.GET)), Times.Once);
        client.Verify(x => x.PutObjectAsync(It.Is<PutObjectRequest>(r => r.Key == Key && r.ContentType == "application/pdf"), It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(x => x.GetObjectAsync(It.Is<GetObjectRequest>(r => r.Key == Key), It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(x => x.DeleteObjectAsync(It.Is<DeleteObjectRequest>(r => r.Key == Key), It.IsAny<CancellationToken>()), Times.Once);
    }
}
