using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.DecideAdaptation;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace CoppAddresd.UnitTests.ProgramProgress.Handlers;

/// <summary>
/// Tests del caso de uso de decisión de adaptaciones (SPEC §5.6, AC-16/AC-17):
/// transiciones Pending → Approved/Rejected, Approved → Applied y el registro
/// de la fila semántica <c>AdaptationApplied</c> en <c>audit.activity_logs</c>.
/// </summary>
public class DecideAdaptationHandlerTests
{
    private readonly FakeProgramRepository _repository = new();
    private readonly DecideAdaptationCommandHandler _handler;
    private readonly Guid _adaptationId = Guid.NewGuid();
    private readonly Guid _enrollmentId = Guid.NewGuid();

    public DecideAdaptationHandlerTests()
    {
        _handler = new DecideAdaptationCommandHandler(
            _repository, NullLogger<DecideAdaptationCommandHandler>.Instance);

        _repository.Adaptations[_adaptationId] = new AdaptationRecommendation
        {
            Id = _adaptationId,
            EnrollmentId = _enrollmentId,
            Kind = AdaptationKind.DifficultyChange,
            TargetEntityType = AdaptationTargetEntityType.WeeklyDayTemplates,
            TargetEntityId = Guid.NewGuid(),
            Payload = JsonSerializer.SerializeToElement(new { points = 100 }),
            Reason = "El paciente superó 5000 XP (AC-16).",
            Status = AdaptationStatus.Pending,
            RequiresApproval = true,
            CreatedAt = DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task Handle_AprobarPending_DevuelveAprobada()
    {
        var command = new DecideAdaptationCommand(_adaptationId, AdaptationDecisionAction.Approve);

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AdaptationStatus.Approved, dto.Status);
        Assert.Equal(_adaptationId, dto.Id);
        // Sin transición a Applied: no hay fila semántica.
        Assert.Empty(_repository.AuditRows);
    }

    [Fact]
    public async Task Handle_RechazarPending_DevuelveRechazada()
    {
        var command = new DecideAdaptationCommand(_adaptationId, AdaptationDecisionAction.Reject, "No indicado");

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AdaptationStatus.Rejected, dto.Status);
        Assert.Empty(_repository.AuditRows);
    }

    [Fact]
    public async Task Handle_ApplyAprobada_RegistraAuditAdaptationApplied()
    {
        // AC-17: al pasar a Applied se escribe la fila semántica
        // action='AdaptationApplied' en audit.activity_logs. Con el flujo
        // atómico (fix B4), la escritura ocurre DENTRO de
        // DecideAdaptationAsync (el fake la registra en AuditRows); el handler
        // ya no audita post-commit, y la aserción verifica que el repositorio
        // la hizo.
        _repository.Adaptations[_adaptationId].Status = AdaptationStatus.Approved;
        var actorId = Guid.NewGuid();

        var command = new DecideAdaptationCommand(_adaptationId, AdaptationDecisionAction.Apply, ActorId: actorId);
        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AdaptationStatus.Applied, dto.Status);
        var audit = Assert.Single(_repository.AuditRows);
        Assert.Equal("AdaptationApplied", audit.Action);
        Assert.Equal("app", audit.SchemaName);
        Assert.Equal("adaptation_recommendations", audit.TableName);
        Assert.Equal(_adaptationId, audit.RecordId);
        Assert.Equal(actorId, audit.ActorId);
    }

    [Fact]
    public async Task Handle_ApplySinAprobacion_LanzaViolacion()
    {
        // Solo las Approved son elegibles para Applied (SPEC §5.6).
        var command = new DecideAdaptationCommand(_adaptationId, AdaptationDecisionAction.Apply);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => _handler.Handle(command, CancellationToken.None));
        Assert.Empty(_repository.AuditRows);
    }

    [Fact]
    public async Task Handle_AdaptacionInexistente_LanzaNotFound()
    {
        var command = new DecideAdaptationCommand(Guid.NewGuid(), AdaptationDecisionAction.Approve);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}