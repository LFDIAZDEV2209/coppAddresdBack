using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Text;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests del proveedor S3 de <see cref="IObjectStorageService"/> con cliente
/// simulado (<c>IAmazonS3</c> mockeado): traducción de errores al contrato
/// común (404 → <c>FileNotFoundException</c>/<c>null</c>), URLs firmadas y
/// fail-fast de configuración. No requiere red ni credenciales AWS.
/// </summary>
public class S3ObjectStorageServiceTests
{
    private const string Bucket = "coppaddresd-test-bucket";

    private static S3ObjectStorageService Build(Mock<IAmazonS3> client)
        => new(client.Object, Bucket);

    private static Mock<IAmazonS3> NewClient()
        => new(MockBehavior.Loose);

    private static AmazonS3Exception NotFoundException()
        => new(
            "El objeto no existe.",
            innerException: null!,
            ErrorType.Sender,
            "NoSuchKey",
            "req-1",
            HttpStatusCode.NotFound,
            "msg-1");

    [Fact]
    public void IsCloudStorage_es_true()
    {
        Assert.True(Build(NewClient()).IsCloudStorage);
    }

    [Fact]
    public void Ctor_falta_bucket_lanza_InvalidOperation()
    {
        var options = Options.Create(new S3StorageOptions { Region = "us-east-2" });
        var ex = Assert.Throws<InvalidOperationException>(
            () => new S3ObjectStorageService(options));
        Assert.Contains("Bucket", ex.Message);
    }

    [Fact]
    public void Ctor_falta_region_lanza_InvalidOperation_sin_crear_cliente()
    {
        var options = Options.Create(new S3StorageOptions { Bucket = Bucket });
        var ex = Assert.Throws<InvalidOperationException>(
            () => new S3ObjectStorageService(options));
        Assert.Contains("Region", ex.Message);
    }

    [Fact]
    public async Task PutObject_envia_bucket_clave_y_content_type_y_devuelve_etag()
    {
        var client = NewClient();
        client
            .Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse { ETag = "\"etag-1\"" });

        var service = Build(client);
        var result = await service.PutObjectAsync(
            "clientes/1/doc.pdf", new MemoryStream(Encoding.UTF8.GetBytes("x")), "application/pdf");

        Assert.Equal("\"etag-1\"", result);
        client.Verify(c => c.PutObjectAsync(
            It.Is<PutObjectRequest>(r =>
                r.BucketName == Bucket &&
                r.Key == "clientes/1/doc.pdf" &&
                r.ContentType == "application/pdf"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetObject_404_se_traduce_a_FileNotFound()
    {
        var client = NewClient();
        client
            .Setup(c => c.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(NotFoundException());

        var service = Build(client);
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => service.GetObjectAsync("no-existe.pdf"));
    }

    [Fact]
    public async Task GetObject_devuelve_stream()
    {
        var client = NewClient();
        client
            .Setup(c => c.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes("contenido")) });

        var service = Build(client);
        await using var stream = await service.GetObjectAsync("a.txt");
        using var reader = new StreamReader(stream);
        Assert.Equal("contenido", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task HeadObject_404_devuelve_null()
    {
        var client = NewClient();
        client
            .Setup(c => c.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(NotFoundException());

        var service = Build(client);
        Assert.Null(await service.HeadObjectAsync("no-existe.txt"));
    }

    [Fact]
    public async Task HeadObject_existente_devuelve_metadata()
    {
        var client = NewClient();
        client
            .Setup(c => c.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectMetadataResponse
            {
                ETag = "\"e\"",
                LastModified = DateTime.UtcNow.AddDays(-1),
            });

        var service = Build(client);
        var meta = await service.HeadObjectAsync("doc.pdf");

        Assert.NotNull(meta);
        Assert.Equal("doc.pdf", meta.Key);
        Assert.Equal("\"e\"", meta.ETag);
    }

    [Fact]
    public async Task ListObjects_mapea_items_y_token()
    {
        var client = NewClient();
        client
            .Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response
            {
                S3Objects = [new S3Object { Key = "agents/b.txt", Size = 2 }, new S3Object { Key = "agents/a.txt", Size = 1 }],
                NextContinuationToken = "tok-1",
            });

        var service = Build(client);
        var result = await service.ListObjectsAsync("agents/");

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(["agents/a.txt", "agents/b.txt"], result.Items.Select(i => i.Key));
        Assert.Equal("tok-1", result.NextContinuationToken);
    }

    [Fact]
    public async Task DeleteObjects_agrupa_en_lotes_de_1000()
    {
        var client = NewClient();
        client
            .Setup(c => c.DeleteObjectsAsync(It.IsAny<DeleteObjectsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectsResponse());

        var service = Build(client);
        var keys = Enumerable.Range(0, 1001).Select(i => $"k/{i}.txt").ToArray();

        await service.DeleteObjectsAsync(keys);

        client.Verify(
            c => c.DeleteObjectsAsync(It.IsAny<DeleteObjectsRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task CopyObject_404_se_traduce_a_FileNotFound()
    {
        var client = NewClient();
        client
            .Setup(c => c.CopyObjectAsync(It.IsAny<CopyObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(NotFoundException());

        var service = Build(client);
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => service.CopyObjectAsync("origen.txt", "destino.txt"));
    }

    [Fact]
    public async Task GetPreSignedUrl_pide_GET_y_expira_en_futuro()
    {
        var client = NewClient();
        client
            .Setup(c => c.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Returns("https://bucket.s3.amazonaws.com/doc.pdf?sig=abc");

        var service = Build(client);
        var url = await service.GetPreSignedUrlAsync("doc.pdf", TimeSpan.FromMinutes(15));

        Assert.Equal("https://bucket.s3.amazonaws.com/doc.pdf?sig=abc", url);
        client.Verify(c => c.GetPreSignedURL(It.Is<GetPreSignedUrlRequest>(r =>
            r.BucketName == Bucket &&
            r.Key == "doc.pdf" &&
            r.Verb == HttpVerb.GET &&
            r.Expires > DateTime.UtcNow.AddMinutes(14))), Times.Once);
    }

    [Fact]
    public async Task GetPreSignedUploadUrl_pide_PUT_con_content_type_cuando_se_provee()
    {
        var client = NewClient();
        client
            .Setup(c => c.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Returns("https://bucket.s3.amazonaws.com/m.mp3?sig=put");

        var service = Build(client);
        var url = await service.GetPreSignedUploadUrlAsync(
            "media/m.mp3", "audio/mpeg", TimeSpan.FromMinutes(15), "http://localhost:5122");

        Assert.Equal("https://bucket.s3.amazonaws.com/m.mp3?sig=put", url);
        client.Verify(c => c.GetPreSignedURL(It.Is<GetPreSignedUrlRequest>(r =>
            r.Verb == HttpVerb.PUT &&
            r.ContentType == "audio/mpeg")), Times.Once);
    }

    [Fact]
    public async Task GetPreSignedUploadUrl_sin_content_type_no_firma_el_tipo()
    {
        var client = NewClient();
        client
            .Setup(c => c.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Returns("https://bucket.s3.amazonaws.com/x?sig=put");

        var service = Build(client);
        await service.GetPreSignedUploadUrlAsync("x", null, TimeSpan.FromMinutes(15), "http://localhost");

        client.Verify(c => c.GetPreSignedURL(It.Is<GetPreSignedUrlRequest>(r =>
            r.Verb == HttpVerb.PUT &&
            string.IsNullOrEmpty(r.ContentType))), Times.Once);
    }
}
