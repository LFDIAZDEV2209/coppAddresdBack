using CoppAddresd.Application.Features.HealthTests.Execution;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.UnitTests.Cache;
using Moq;

namespace CoppAddresd.UnitTests.Features.HealthTests;

public sealed class MasterAssignmentRegressionTests
{
    [Theory]
    [InlineData(HealthTestAssignmentStatus.pending, "pendiente")]
    [InlineData(HealthTestAssignmentStatus.in_progress, "en-progreso")]
    public async Task Handle_ReassignmentWithHistoricalCompletion_RemainsOpenWithRealDate(
        HealthTestAssignmentStatus status, string expectedState)
    {
        var patient = new PatientProfile { Id = Guid.NewGuid(), FirstName = "QA", LastName = "Técnico" };
        var versionId = Guid.NewGuid();
        var oldDate = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);
        var newDate = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);
        var old = new HealthTestAssignment
        {
            PatientId = patient.Id, Patient = patient, VersionId = versionId,
            AssignedAt = oldDate, Status = HealthTestAssignmentStatus.completed,
            Evaluations = [new() { Status = HealthTestEvaluationStatus.completed, CompletedAt = oldDate }]
        };
        var current = new HealthTestAssignment
        {
            PatientId = patient.Id, Patient = patient, VersionId = versionId,
            AssignedAt = newDate, Status = status
        };
        var result = Assert.Single(Assert.Single(await Query(old, current)).Results);
        Assert.Equal(expectedState, result.State);
        Assert.Equal(newDate, result.PendingAssignedAt);
        Assert.Equal(oldDate, result.CompletedAt);
        Assert.Single(result.History!);
    }

    [Fact]
    public async Task Handle_AllAssignmentsCompleted_HasNoPendingDate()
    {
        var patient = new PatientProfile { Id = Guid.NewGuid() };
        var date = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        var assignment = new HealthTestAssignment
        {
            PatientId = patient.Id, Patient = patient, VersionId = Guid.NewGuid(),
            AssignedAt = date, Status = HealthTestAssignmentStatus.completed,
            Evaluations = [new() { Status = HealthTestEvaluationStatus.completed, CompletedAt = date }]
        };
        var result = Assert.Single(Assert.Single(await Query(assignment)).Results);
        Assert.Equal("completado", result.State);
        Assert.Null(result.PendingAssignedAt);
    }

    private static Task<IReadOnlyList<MasterPatientRowDto>> Query(params HealthTestAssignment[] assignments)
    {
        var repo = new Mock<IHealthTestRepository>();
        repo.Setup(r => r.ListAssignmentsWithPatientDataForZoneAsync(null, It.IsAny<IReadOnlyCollection<string>>(), null, default)).ReturnsAsync(assignments);
        repo.Setup(r => r.ListActiveAlertCountsForZoneAsync(It.IsAny<IReadOnlyCollection<string>>(), null, default)).ReturnsAsync(new Dictionary<Guid, int>());
        repo.Setup(r => r.ListProfessionalNamesByPatientAsync(default)).ReturnsAsync(new Dictionary<Guid, string>());
        repo.Setup(r => r.ListClinicNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default)).ReturnsAsync(new Dictionary<Guid, string>());
        repo.Setup(r => r.ListInsurerNamesByIdsAsync(It.IsAny<IEnumerable<Guid>>(), default)).ReturnsAsync(new Dictionary<Guid, string>());
        return new GetMasterRowsQueryHandler(repo.Object, new FakeCacheService()).Handle(new(), default);
    }
}
