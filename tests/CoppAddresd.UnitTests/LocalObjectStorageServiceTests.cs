using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Options;
using System.Text;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests del proveedor Local de <see cref="IObjectStorageService"/>:
/// contrato completo (put/get/head/list/delete/copy/URLs), guardias de
/// seguridad (path traversal, nombres de dispositivo NTFS) y el fix de
/// <c>RootPath</c> vacío (no lanza <c>Path.GetFullPath("")</c>).
/// </summary>
public class LocalObjectStorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "coppaddresd-unit-" + Guid.NewGuid().ToString("N"));

    public LocalObjectStorageServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // El directorio ya no existe o no pudo eliminarse: no enmascarar.
        }
    }

    private LocalObjectStorageService Build(string? rootPath = null)
        => new(Options.Create(new LocalStorageOptions { RootPath = rootPath ?? _root }));

    private static MemoryStream Stream(string content)
        => new(Encoding.UTF8.GetBytes(content));

    [Fact]
    public void IsCloudStorage_es_false()
    {
        Assert.False(Build().IsCloudStorage);
    }

    [Fact]
    public async Task Ctor_con_RootPath_vacio_no_lanza_GetFullPath_vacio()
    {
        // Regresión Fase 2: RootPath vacío resuelve al directorio de trabajo, no lanza.
        var service = Build(rootPath: "");
        var path = await service.PutObjectAsync("a.txt", Stream("x"));
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task PutObject_crea_archivo_y_devuelve_ruta_absoluta()
    {
        var service = Build();
        var path = await service.PutObjectAsync("clientes/123/doc.pdf", Stream("hola"));

        Assert.True(File.Exists(path));
        Assert.True(Path.IsPathRooted(path));
        Assert.EndsWith($"clientes{Path.DirectorySeparatorChar}123{Path.DirectorySeparatorChar}doc.pdf", path);
        Assert.Equal("hola", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task PutObject_sobrescribe_y_no_deja_temporales()
    {
        var service = Build();
        await service.PutObjectAsync("x.txt", Stream("v1"));
        await service.PutObjectAsync("x.txt", Stream("v2"));

        Assert.Equal("v2", await File.ReadAllTextAsync(Path.Combine(_root, "x.txt")));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp-*"));
    }

    [Fact]
    public async Task GetObject_devuelve_contenido()
    {
        var service = Build();
        await service.PutObjectAsync("d.md", Stream("# título"));

        await using var stream = await service.GetObjectAsync("d.md");
        using var reader = new StreamReader(stream);
        Assert.Equal("# título", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task GetObject_key_inexistente_lanza_FileNotFound()
    {
        var service = Build();
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => service.GetObjectAsync("no-existe.txt"));
    }

    [Fact]
    public async Task HeadObject_existente_devuelve_metadata()
    {
        var service = Build();
        await service.PutObjectAsync("m/podcast.mp3", Stream("audio"), "audio/mpeg");

        var meta = await service.HeadObjectAsync("m/podcast.mp3");

        Assert.NotNull(meta);
        Assert.Equal("m/podcast.mp3", meta.Key);
        Assert.Equal(5, meta.Size);
        Assert.Equal("audio/mpeg", meta.ContentType);
    }

    [Fact]
    public async Task HeadObject_inexistente_devuelve_null()
    {
        var service = Build();
        Assert.Null(await service.HeadObjectAsync("nada.txt"));
    }

    [Fact]
    public async Task ListObjects_filtra_por_prefijo_y_devuelve_token_cuando_queda_mas()
    {
        var service = Build();
        await service.PutObjectAsync("agents/a1.txt", Stream("a"));
        await service.PutObjectAsync("agents/a2.txt", Stream("b"));
        await service.PutObjectAsync("media/m1.mp3", Stream("c"));

        var result = await service.ListObjectsAsync("agents/");

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, i => i.Key == "agents/a1.txt");
        Assert.DoesNotContain(result.Items, i => i.Key == "media/m1.mp3");
    }

    [Fact]
    public async Task DeleteObject_elimina_y_no_op_si_no_existe()
    {
        var service = Build();
        await service.PutObjectAsync("borrar.txt", Stream("x"));

        await service.DeleteObjectAsync("borrar.txt");
        Assert.False(File.Exists(Path.Combine(_root, "borrar.txt")));

        await service.DeleteObjectAsync("borrar.txt"); // no-op
    }

    [Fact]
    public async Task DeleteObjects_elimina_en_lote_tolerando_ausentes()
    {
        var service = Build();
        await service.PutObjectAsync("a.txt", Stream("1"));
        await service.PutObjectAsync("b.txt", Stream("2"));

        await service.DeleteObjectsAsync(new[] { "a.txt", "b.txt", "no-existe.txt" });

        Assert.False(File.Exists(Path.Combine(_root, "a.txt")));
        Assert.False(File.Exists(Path.Combine(_root, "b.txt")));
    }

    [Fact]
    public async Task CopyObject_copia_sobrescribiendo_destino()
    {
        var service = Build();
        await service.PutObjectAsync("src.txt", Stream("contenido"));
        await service.PutObjectAsync("dst.txt", Stream("viejo"));

        await service.CopyObjectAsync("src.txt", "dst.txt");

        Assert.Equal("contenido", await File.ReadAllTextAsync(Path.Combine(_root, "dst.txt")));
    }

    [Fact]
    public async Task CopyObject_origen_inexistente_lanza_FileNotFound()
    {
        var service = Build();
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => service.CopyObjectAsync("origen-faltante.txt", "dst.txt"));
    }

    [Fact]
    public async Task GetPreSignedUrl_devuelve_ruta_absoluta()
    {
        var service = Build();
        await service.PutObjectAsync("media/x.mp3", Stream("m"));

        var url = await service.GetPreSignedUrlAsync("media/x.mp3", TimeSpan.FromMinutes(15));

        Assert.True(Path.IsPathRooted(url));
        Assert.True(File.Exists(url));
    }

    [Fact]
    public async Task GetPreSignedUploadUrl_devuelve_url_del_proxy_del_backend()
    {
        var service = Build();

        var url = await service.GetPreSignedUploadUrlAsync(
            "media/f/abc.mp3", "audio/mpeg", TimeSpan.FromMinutes(15), "http://localhost:5122");

        Assert.Equal("http://localhost:5122/api/v1/storage/media/f/abc.mp3", url);
    }

    [Fact]
    public async Task PutObject_path_traversal_se_rechaza()
    {
        var service = Build();
        var ex = await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.PutObjectAsync("../escape.txt", Stream("x")));
        Assert.Contains("escapar", ex.Message);
        Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), "escape.txt")));
    }

    [Fact]
    public async Task PutObject_nombre_dispositivo_reservado_se_rechaza_en_windows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var service = Build();
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.PutObjectAsync("NUL", Stream("x")));
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.PutObjectAsync("docs/CON.txt", Stream("x")));
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.PutObjectAsync("archivo.txt:stream", Stream("x")));
    }
}
