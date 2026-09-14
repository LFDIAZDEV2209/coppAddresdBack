using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.UnitTests.ProgramProgress.ProgramControls;

/// <summary>
/// Pruebas del read model puro de Controles (UC-004): orden de la línea de
/// tiempo, control abierto vigente, próximo vencimiento, contadores de
/// adherencia, día sin fila como pending y passthrough del motivo de cierre.
/// </summary>
public class ProgramControlProgressTests
{
    private static readonly DateOnly Start = new(2026, 1, 5);

    private static readonly IReadOnlyList<int> Days = [7, 14, 21, 45, 60, 90];

    private static readonly DateTime Now = new(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Build_DiasDesordenados_OrdenaAscendenteYCalculaFechaObjetivo()
    {
        var snapshot = ProgramControlProgress.Build([], [45, 7, 90, 21], Start, Now);

        Assert.Equal(
            new[] { 7, 21, 45, 90 },
            snapshot.Milestones.Select(m => m.MilestoneDay).ToArray()
        );
        // Día 1 = fecha de inicio: el día 7 cae en start + 6 y el 45 en start + 44.
        Assert.Equal(Start.AddDays(6), snapshot.Milestones[0].TargetDate);
        Assert.Equal(Start.AddDays(44), snapshot.Milestones[2].TargetDate);
    }

    [Fact]
    public void Build_DiaSinFila_DevuelvePendingConCamposNulos()
    {
        var snapshot = ProgramControlProgress.Build([], Days, Start, Now);

        Assert.Equal(Days.Count, snapshot.Milestones.Count);
        Assert.All(
            snapshot.Milestones,
            milestone =>
            {
                Assert.Equal("pending", milestone.Status);
                Assert.Null(milestone.ControlId);
                Assert.Null(milestone.SentAt);
                Assert.Null(milestone.RespondedAt);
                Assert.Null(milestone.FollowupSentAt);
                Assert.Null(milestone.CompletedAt);
                Assert.Null(milestone.ClosedReason);
            }
        );
    }

    [Fact]
    public void Build_ControlVigente_EsElAbiertoMasRecientePorSentAt()
    {
        var older = NewControl(
            7,
            ProgramControlStatus.FollowedUp,
            sentAt: new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc)
        );
        var newer = NewControl(
            14,
            ProgramControlStatus.Responded,
            sentAt: new DateTime(2026, 1, 20, 8, 0, 0, DateTimeKind.Utc)
        );
        // Terminal con SentAt posterior: nunca compite por ser el vigente.
        var closed = NewControl(
            21,
            ProgramControlStatus.Completed,
            sentAt: new DateTime(2026, 1, 25, 8, 0, 0, DateTimeKind.Utc)
        );

        var snapshot = ProgramControlProgress.Build([older, closed, newer], Days, Start, Now);

        Assert.NotNull(snapshot.CurrentControl);
        Assert.Equal(newer.Id, snapshot.CurrentControl!.ControlId);
        Assert.Equal(14, snapshot.CurrentControl.MilestoneDay);
        Assert.Equal("responded", snapshot.CurrentControl.Status);
        Assert.Equal(newer.SentAt, snapshot.CurrentControl.SentAt);
    }

    [Fact]
    public void Build_SinControlesAbiertos_ControlVigenteEsNull()
    {
        var completed = NewControl(7, ProgramControlStatus.Completed);
        var missed = NewControl(14, ProgramControlStatus.Missed);

        var snapshot = ProgramControlProgress.Build([completed, missed], Days, Start, Now);

        Assert.Null(snapshot.CurrentControl);
    }

    [Fact]
    public void Build_NextDue_EsElMenorDiaSinFila()
    {
        var control = NewControl(7, ProgramControlStatus.Sent);

        var snapshot = ProgramControlProgress.Build([control], Days, Start, Now);

        Assert.NotNull(snapshot.NextDue);
        Assert.Equal(14, snapshot.NextDue!.MilestoneDay);
        Assert.Equal(Start.AddDays(13), snapshot.NextDue.TargetDate);
    }

    [Fact]
    public void Build_TodosLosDiasConFila_NextDueEsNull()
    {
        var controls = Days.Select(day => NewControl(day, ProgramControlStatus.Completed)).ToList();

        var snapshot = ProgramControlProgress.Build(controls, Days, Start, Now);

        Assert.Null(snapshot.NextDue);
    }

    [Fact]
    public void Build_Adherencia_CuentaEstadosDiasSinFilaYEnvios()
    {
        var completed = NewControl(
            7,
            ProgramControlStatus.Completed,
            sentAt: new DateTime(2026, 1, 11, 8, 0, 0, DateTimeKind.Utc),
            completedAt: new DateTime(2026, 1, 12, 9, 0, 0, DateTimeKind.Utc)
        );
        var missed = NewControl(
            14,
            ProgramControlStatus.Missed,
            sentAt: new DateTime(2026, 1, 18, 8, 0, 0, DateTimeKind.Utc),
            followupSentAt: new DateTime(2026, 1, 20, 8, 0, 0, DateTimeKind.Utc)
        );
        var closed = NewControl(
            21,
            ProgramControlStatus.ClosedWithoutExam,
            sentAt: new DateTime(2026, 1, 25, 8, 0, 0, DateTimeKind.Utc),
            closedReason: "declined"
        );
        var followedUp = NewControl(
            45,
            ProgramControlStatus.FollowedUp,
            sentAt: new DateTime(2026, 2, 18, 8, 0, 0, DateTimeKind.Utc),
            followupSentAt: new DateTime(2026, 2, 20, 8, 0, 0, DateTimeKind.Utc)
        );
        var responded = NewControl(
            60,
            ProgramControlStatus.Responded,
            sentAt: new DateTime(2026, 3, 5, 8, 0, 0, DateTimeKind.Utc),
            respondedAt: new DateTime(2026, 3, 6, 8, 0, 0, DateTimeKind.Utc)
        );
        // Día 90 configurado sin fila: cuenta como pending.

        var snapshot = ProgramControlProgress.Build(
            [completed, missed, closed, followedUp, responded],
            Days,
            Start,
            Now
        );

        var adherence = snapshot.Adherence;
        Assert.Equal(1, adherence.Completed);
        Assert.Equal(1, adherence.Missed);
        Assert.Equal(1, adherence.ClosedWithoutExam);
        Assert.Equal(1, adherence.Pending);
        Assert.Equal(1, adherence.Responded);
        Assert.Equal(2, adherence.FollowupsSent);
        Assert.Equal(5, adherence.MessagesSent);
    }

    [Fact]
    public void Build_ClosedReason_PasaElMotivoDeCierreEnLaLineaDeTiempo()
    {
        var closed = NewControl(
            21,
            ProgramControlStatus.ClosedWithoutExam,
            sentAt: new DateTime(2026, 1, 25, 8, 0, 0, DateTimeKind.Utc),
            closedReason: "no_upload_timeout"
        );

        var snapshot = ProgramControlProgress.Build([closed], [21], Start, Now);

        var milestone = Assert.Single(snapshot.Milestones);
        Assert.Equal(21, milestone.MilestoneDay);
        Assert.Equal("closed_without_exam", milestone.Status);
        Assert.Equal(closed.Id, milestone.ControlId);
        Assert.Equal("no_upload_timeout", milestone.ClosedReason);
        Assert.Equal(closed.SentAt, milestone.SentAt);
    }

    [Theory]
    [InlineData(ProgramControlStatus.Pending, "pending")]
    [InlineData(ProgramControlStatus.Sent, "sent")]
    [InlineData(ProgramControlStatus.Responded, "responded")]
    [InlineData(ProgramControlStatus.FollowedUp, "followed_up")]
    [InlineData(ProgramControlStatus.Completed, "completed")]
    [InlineData(ProgramControlStatus.ClosedWithoutExam, "closed_without_exam")]
    [InlineData(ProgramControlStatus.Missed, "missed")]
    [InlineData(ProgramControlStatus.Failed, "failed")]
    [InlineData(ProgramControlStatus.Skipped, "skipped")]
    public void StatusName_MapeaTodosLosEstadosAlContrato(ProgramControlStatus status, string expected)
    {
        Assert.Equal(expected, ProgramControlProgress.StatusName(status));
    }

    /// <summary>Fila de control mínima con solo los campos del caso.</summary>
    private static ProgramControl NewControl(
        int milestoneDay,
        ProgramControlStatus status,
        DateTime? sentAt = null,
        DateTime? respondedAt = null,
        DateTime? followupSentAt = null,
        DateTime? completedAt = null,
        string? closedReason = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            EnrollmentId = Guid.NewGuid(),
            MilestoneDay = milestoneDay,
            Status = status,
            SentAt = sentAt,
            RespondedAt = respondedAt,
            FollowupSentAt = followupSentAt,
            CompletedAt = completedAt,
            ClosedReason = closedReason,
            CreatedAt = DateTime.UtcNow,
        };
}
