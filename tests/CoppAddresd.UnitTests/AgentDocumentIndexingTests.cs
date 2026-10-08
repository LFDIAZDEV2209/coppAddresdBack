using System.Text;
using CoppAddresd.Application.Features.Agents;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CoppAddresd.UnitTests;

/// <summary>Regresiones de indexación: reintento, reserva, fallo y cancelación del cliente.</summary>
public sealed class AgentDocumentIndexingTests
{
    private sealed class Fixture
    {
        public Mock<IAgentCatalogRepository> Repository { get; } = new();
        public Mock<IObjectStorageService> Storage { get; } = new();
        public Mock<IAgentRuntimeSyncService> Runtime { get; } = new();
        public AgentDocument Document { get; } = new()
        {
            Id = Guid.NewGuid(), KnowledgeBaseId = Guid.NewGuid(), FileName = "guide.md",
            StorageKey = "agents/docs/guide.md", Status = AgentDocumentStatus.Procesando,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1), ErrorMessage = "Anterior",
        };
        public AgentDocumentIndexer Indexer { get; }
        public Fixture()
        {
            Repository.Setup(x => x.TryStartDocumentIndexAsync(It.IsAny<Guid>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            Repository.Setup(x => x.CompleteDocumentIndexAsync(It.IsAny<AgentDocument>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Repository.Setup(x => x.GetDocumentAsync(Document.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Document);
            Storage.Setup(x => x.GetObjectAsync(Document.StorageKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes("contenido original")));
            Runtime.Setup(x => x.IngestDocumentAsync(It.IsAny<AgentDocumentIngestPayload>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentDocumentIngestResult("indexado", 4, 0, null));
            Indexer = new(Repository.Object, Storage.Object, Runtime.Object,
                NullLogger<AgentDocumentIndexer>.Instance);
        }
    }

    [Fact]
    public async Task Retry_ReutilizaDocumentoBlobEIds_YReemplazaError()
    {
        var f = new Fixture();
        var handler = new RetryAgentDocumentCommandHandler(f.Repository.Object, f.Indexer);
        var result = await handler.Handle(new(f.Document.Id), CancellationToken.None);
        Assert.Equal(f.Document.Id, result.Id);
        Assert.Equal(AgentDocumentStatus.Listo, result.Status);
        Assert.Equal(4, result.ChunksCount);
        Assert.Null(result.ErrorMessage);
        f.Runtime.Verify(x => x.IngestDocumentAsync(It.Is<AgentDocumentIngestPayload>(p =>
            p.DocumentId == f.Document.Id && p.KnowledgeBaseId == f.Document.KnowledgeBaseId
            && Encoding.UTF8.GetString(Convert.FromBase64String(p.ContentBase64)) == "contenido original"),
            It.IsAny<CancellationToken>()), Times.Once);
        f.Repository.Verify(x => x.AddDocumentAsync(It.IsAny<AgentDocument>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancelacion_PersisteEstadoRecuperableConTokenPropio_YSePropaga()
    {
        var f = new Fixture();
        using var cts = new CancellationTokenSource();
        f.Storage.Setup(x => x.GetObjectAsync(f.Document.StorageKey, It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken token) =>
            {
                cts.Cancel();
                return Task.FromCanceled<Stream>(token);
            });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Indexer.IndexAsync(f.Document, cts.Token));
        Assert.Equal(AgentDocumentStatus.Error, f.Document.Status);
        Assert.Contains("interrumpida", f.Document.ErrorMessage);
        f.Repository.Verify(x => x.CompleteDocumentIndexAsync(f.Document, It.IsAny<DateTimeOffset>(),
            It.Is<CancellationToken>(token => token.CanBeCanceled && !token.IsCancellationRequested)), Times.Once);
    }

    [Fact]
    public async Task ReservaOcupada_NoIniciaOtraIndexacion()
    {
        var f = new Fixture();
        f.Repository.Setup(x => x.TryStartDocumentIndexAsync(f.Document.Id,
            It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => f.Indexer.IndexAsync(f.Document, CancellationToken.None));
        f.Storage.Verify(x => x.GetObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StorageFalla_NoQuedaProcesando_NiExponeRutaInterna()
    {
        var f = new Fixture();
        f.Storage.Setup(x => x.GetObjectAsync(f.Document.StorageKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("/private/storage/secret"));
        await f.Indexer.IndexAsync(f.Document, CancellationToken.None);
        Assert.Equal(AgentDocumentStatus.Error, f.Document.Status);
        Assert.DoesNotContain("/private", f.Document.ErrorMessage);
        f.Repository.Verify(x => x.CompleteDocumentIndexAsync(f.Document, It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IndexadoSinChunks_NoSePresentaComoListo()
    {
        var f = new Fixture();
        f.Runtime.Setup(x => x.IngestDocumentAsync(It.IsAny<AgentDocumentIngestPayload>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(new AgentDocumentIngestResult("indexado", 0, 0, null));
        await f.Indexer.IndexAsync(f.Document, CancellationToken.None);
        Assert.Equal(AgentDocumentStatus.Error, f.Document.Status);
        Assert.NotNull(f.Document.ErrorMessage);
    }

    [Fact]
    public async Task ErrorDelRuntime_SeGuardaParaReintentar()
    {
        var f = new Fixture();
        f.Runtime.Setup(x => x.IngestDocumentAsync(It.IsAny<AgentDocumentIngestPayload>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(new AgentDocumentIngestResult("error", 0, 0, "Fallo interno"));
        await f.Indexer.IndexAsync(f.Document, CancellationToken.None);
        Assert.Equal(AgentDocumentStatus.Error, f.Document.Status);
        Assert.NotNull(f.Document.ErrorMessage);
    }

    [Fact]
    public async Task Retry_DocumentoInexistente_NoIniciaPipeline()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new RetryAgentDocumentCommandHandler(f.Repository.Object, f.Indexer)
                .Handle(new(Guid.NewGuid()), CancellationToken.None));
    }
}
