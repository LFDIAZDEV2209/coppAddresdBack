using CoppAddresd.Api.Controllers;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.UnitTests.ProgramProgress.ProgramControls;

/// <summary>
/// Pruebas del mapeo de <see cref="ProgramControlSendDto"/> (historial de
/// controles): la fila expone los campos del ciclo de vida de la fase 2
/// (respuesta, follow-up, completación, motivo de cierre y lote de examen)
/// además de los de la fase 1.
/// </summary>
public class ProgramControlSendDtoTests
{
    [Fact]
    public void FromEntity_MapeaCamposDeCicloDeVidaYLoteDeExamen()
    {
        var enrollment = new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
        };
        var batchId = Guid.NewGuid();
        var sentAt = new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc);
        var respondedAt = new DateTime(2026, 1, 11, 9, 0, 0, DateTimeKind.Utc);
        var followupSentAt = new DateTime(2026, 1, 13, 8, 0, 0, DateTimeKind.Utc);
        var completedAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var createdAt = new DateTime(2026, 1, 8, 7, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var control = new ProgramControl
        {
            Id = Guid.NewGuid(),
            EnrollmentId = enrollment.Id,
            Enrollment = enrollment,
            MilestoneDay = 7,
            Status = ProgramControlStatus.ClosedWithoutExam,
            Attempts = 2,
            ThreadId = "thread-123",
            SentAt = sentAt,
            RespondedAt = respondedAt,
            FollowupSentAt = followupSentAt,
            CompletedAt = completedAt,
            ClosedReason = "declined",
            ExamBatchId = batchId,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };

        var dto = ProgramControlSendDto.FromEntity(control);

        Assert.Equal(control.Id, dto.Id);
        Assert.Equal(enrollment.Id, dto.EnrollmentId);
        Assert.Equal(enrollment.PatientId, dto.PatientId);
        Assert.Equal(7, dto.MilestoneDay);
        Assert.Equal(ProgramControlStatus.ClosedWithoutExam, dto.Status);
        Assert.Equal(2, dto.Attempts);
        Assert.Equal("thread-123", dto.ThreadId);
        Assert.Equal(sentAt, dto.SentAt);
        Assert.Equal(createdAt, dto.CreatedAt);
        Assert.Equal(updatedAt, dto.UpdatedAt);
        Assert.Equal(batchId, dto.ExamBatchId);
        Assert.Equal(respondedAt, dto.RespondedAt);
        Assert.Equal(followupSentAt, dto.FollowupSentAt);
        Assert.Equal(completedAt, dto.CompletedAt);
        Assert.Equal("declined", dto.ClosedReason);
    }

    [Fact]
    public void FromEntity_ControlSoloFase1_DejaNullElCicloDeVida()
    {
        var enrollment = new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
        };
        var control = new ProgramControl
        {
            Id = Guid.NewGuid(),
            EnrollmentId = enrollment.Id,
            Enrollment = enrollment,
            MilestoneDay = 14,
            Status = ProgramControlStatus.Sent,
            SentAt = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc),
        };

        var dto = ProgramControlSendDto.FromEntity(control);

        Assert.Equal(ProgramControlStatus.Sent, dto.Status);
        Assert.Null(dto.ExamBatchId);
        Assert.Null(dto.RespondedAt);
        Assert.Null(dto.FollowupSentAt);
        Assert.Null(dto.CompletedAt);
        Assert.Null(dto.ClosedReason);
    }
}
