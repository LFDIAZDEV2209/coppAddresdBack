using CoppAddresd.Application.Features.HealthTests;
using CoppAddresd.Application.Features.HealthTests.Catalog;
using CoppAddresd.Application.Features.HealthTests.Notifications;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.HealthTests;
using Moq;
namespace CoppAddresd.UnitTests.Features.HealthTests;
public sealed class CatalogAndReminderRegressionTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Publish_UsesAtomicTransitionAndDoesNotReportSuccessOnConflict(bool published)
    {
        var version = new HealthTestVersion { Id = Guid.NewGuid(), InstrumentId = Guid.NewGuid(), Status = HealthTestVersionStatus.draft,
            Questions = [new() { Id = Guid.NewGuid(), Code = "q", Text = "QA", Type = HealthTestQuestionType.open, IsActive = true }] };
        var repo = new Mock<IHealthTestRepository>();
        repo.Setup(r => r.GetVersionWithDetailsAsync(version.Id, default)).ReturnsAsync(version);
        repo.Setup(r => r.TryPublishVersionAsync(version.Id, version.InstrumentId, It.IsAny<DateTime>(), default)).ReturnsAsync(published);
        var handler = new PublishVersionCommandHandler(repo.Object);
        if (!published) await Assert.ThrowsAsync<CoppAddresd.Domain.Exceptions.BusinessRuleViolationException>(() => handler.Handle(new(version.Id), default));
        else { var result = await handler.Handle(new(version.Id), default); Assert.Equal(HealthTestVersionStatus.active, result!.Status); Assert.True(result.IsCurrent); }
        repo.Verify(r => r.UpdateVersionAsync(It.IsAny<HealthTestVersion>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Clone_PreservesNumericSettingsAndRemapsConditionalOptions()
    {
        var q1 = new HealthTestQuestion { Id = Guid.NewGuid(), Code = "numeric", Type = HealthTestQuestionType.num,
            Unit = "kg", MinValue = 10, MaxValue = 200, DefaultValue = 70, MinLabel = "mínimo", MaxLabel = "máximo", Hint = "pista" };
        var option = new HealthTestAnswerOption { Id = Guid.NewGuid(), QuestionId = q1.Id, Text = "A" };
        q1.Options.Add(option);
        var q2 = new HealthTestQuestion { Id = Guid.NewGuid(), Code = "conditional", Options = [new() { Id = Guid.NewGuid(), Text = "B", DependsOnQuestionId = q1.Id, DependsOnOptionId = option.Id }] };
        var source = new HealthTestVersion { Id = Guid.NewGuid(), InstrumentId = Guid.NewGuid(), Questions = [q1, q2] };
        var repo = new Mock<IHealthTestRepository>();
        repo.Setup(r => r.GetVersionWithDetailsAsync(source.Id, default)).ReturnsAsync(source);
        repo.Setup(r => r.GetNextVersionNumberAsync(source.InstrumentId, default)).ReturnsAsync(2);
        IReadOnlyList<HealthTestQuestion>? saved = null;
        repo.Setup(r => r.AddQuestionsRangeAsync(It.IsAny<IEnumerable<HealthTestQuestion>>(), default)).Callback<IEnumerable<HealthTestQuestion>, CancellationToken>((qs, _) => saved = qs.ToList()).Returns(Task.CompletedTask);
        await new CloneVersionCommandHandler(repo.Object).Handle(new(source.Id), default);
        var numeric = Assert.Single(saved!, q => q.Code == "numeric");
        Assert.Equal("kg", numeric.Unit); Assert.Equal(10, numeric.MinValue); Assert.Equal(200, numeric.MaxValue); Assert.Equal(70, numeric.DefaultValue); Assert.Equal("pista", numeric.Hint);
        var conditional = Assert.Single(saved!, q => q.Code == "conditional");
        Assert.Equal(numeric.Id, conditional.Options.Single().DependsOnQuestionId);
        Assert.Equal(numeric.Options.Single().Id, conditional.Options.Single().DependsOnOptionId);
        Assert.NotEqual(q1.Id, numeric.Id); Assert.Equal(q1.Id, source.Questions.First().Id);
    }
    [Fact]
    public async Task SaveQuestion_RejectsPublishedVersionBeforeWriting()
    {
        var version = new HealthTestVersion { Id = Guid.NewGuid(), Status = HealthTestVersionStatus.active };
        var repo = new Mock<IHealthTestRepository>();
        repo.Setup(r => r.GetVersionWithDetailsAsync(version.Id, default)).ReturnsAsync(version);
        var question = new HealthTestQuestion { Code = "q", Text = "Pregunta" };
        await Assert.ThrowsAsync<CoppAddresd.Domain.Exceptions.BusinessRuleViolationException>(() => new SaveDraftQuestionCommandHandler(repo.Object).Handle(new(version.Id, HealthTestQuestionDto.FromEntity(question)), default));
        repo.Verify(r => r.SaveQuestionAsync(It.IsAny<HealthTestQuestion>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task Reminder_NoPendingAssignmentsDoesNotCreateNotification()
    {
        var tests = new Mock<IHealthTestRepository>(); var notifications = new Mock<INotificationLogRepository>(); var id = Guid.NewGuid();
        tests.Setup(r => r.ListActiveAssignmentsByPatientAsync(id, default)).ReturnsAsync([]);
        var result = await new SendHealthTestReminderCommandHandler(tests.Object, notifications.Object).Handle(new(id), default);
        Assert.False(result.Sent);
        notifications.Verify(r => r.TryAddDailyReminderAsync(It.IsAny<AppNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Reminder_ReportsPersistedOutcomeAndTargetsPatient(bool persisted)
    {
        var tests = new Mock<IHealthTestRepository>(); var notifications = new Mock<INotificationLogRepository>(); var id = Guid.NewGuid();
        tests.Setup(r => r.ListActiveAssignmentsByPatientAsync(id, default)).ReturnsAsync([new() { Status = HealthTestAssignmentStatus.pending }]);
        notifications.Setup(r => r.TryAddDailyReminderAsync(It.Is<AppNotification>(n => n.PatientId == id && n.Type == "health_test_reminder" && n.Channel == "inapp"), default)).ReturnsAsync(persisted);
        var result = await new SendHealthTestReminderCommandHandler(tests.Object, notifications.Object).Handle(new(id), default);
        Assert.Equal(persisted, result.Sent);
    }
}
