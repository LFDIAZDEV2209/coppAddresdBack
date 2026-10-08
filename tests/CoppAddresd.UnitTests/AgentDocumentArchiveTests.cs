using CoppAddresd.Application.Features.Agents;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;
using Moq;
namespace CoppAddresd.UnitTests;
public sealed class AgentDocumentArchiveTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Archivar_RespetaReservaConcurrenteYConservaIdentidad(bool reserved)
    {
        var doc = new AgentDocument { Id = Guid.NewGuid(), KnowledgeBaseId = Guid.NewGuid(), Status = AgentDocumentStatus.Listo, FileName = "qa.md", StorageKey = "qa.md" };
        var repo = new Mock<IAgentCatalogRepository>();
        repo.Setup(r => r.GetDocumentAsync(doc.Id, default)).ReturnsAsync(doc);
        repo.Setup(r => r.TryArchiveDocumentAsync(doc.Id, It.IsAny<DateTimeOffset>(), default)).ReturnsAsync(reserved);
        var handler = new ArchiveAgentDocumentCommandHandler(repo.Object);
        if (!reserved) await Assert.ThrowsAsync<BusinessRuleViolationException>(() => handler.Handle(new(doc.Id), default));
        else { var result = await handler.Handle(new(doc.Id), default); Assert.Equal(doc.Id, result.Id); Assert.Equal(AgentDocumentStatus.Archivado, result.Status); }
        repo.Verify(r => r.DeleteDocumentAsync(It.IsAny<AgentDocument>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.UpdateDocumentAsync(It.IsAny<AgentDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
