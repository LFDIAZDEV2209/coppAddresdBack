using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Fábrica de alertas (AlertMaterializer): cada evento de dominio materializa la
/// alerta correcta (tipo, severidad, destinatario, cita relacionada) o devuelve
/// <c>null</c> cuando no hay destinatario resoluble.
/// </summary>
public class AlertMaterializerTests
{
    private static readonly Guid Recipient = TestData.UserId;
    private static readonly Guid AppointmentId = Guid.NewGuid();
    private static readonly Guid RequestId = Guid.NewGuid();

    [Fact]
    public void NewRequest_ConstruyeAlertaInfo()
    {
        var alert = AlertMaterializer.NewRequest(
            Recipient,
            RequestId,
            TestData.SpecialtyId,
            "María",
            "Medicina General"
        );

        Assert.NotNull(alert);
        Assert.Equal(AlertType.NewRequest, alert!.Type);
        Assert.Equal(AlertSeverity.Info, alert.Severity);
        Assert.Equal(Recipient, alert.RecipientUserId);
        Assert.Equal(AlertRecipientType.User, alert.RecipientType);
        Assert.Null(alert.RelatedAppointmentId);
        Assert.Contains("María", alert.Body);
    }

    [Fact]
    public void RequestApproved_ConstruyeAlertaInfo()
    {
        var alert = AlertMaterializer.RequestApproved(
            Recipient,
            RequestId,
            TestData.SpecialtyId,
            "María",
            "Medicina General"
        );

        Assert.NotNull(alert);
        Assert.Equal(AlertType.RequestApproved, alert!.Type);
        Assert.Equal(AlertSeverity.Info, alert.Severity);
        Assert.Equal(Recipient, alert.RecipientUserId);
        Assert.Null(alert.RelatedAppointmentId);
        Assert.Contains("María", alert.Body);
    }

    [Fact]
    public void RequestRejected_EsWarningEIncluyeMotivo()
    {
        var alert = AlertMaterializer.RequestRejected(
            Recipient,
            RequestId,
            TestData.SpecialtyId,
            "Sin cupos"
        );

        Assert.NotNull(alert);
        Assert.Equal(AlertType.RequestRejected, alert!.Type);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
        Assert.Equal(Recipient, alert.RecipientUserId);
        Assert.Contains("Sin cupos", alert.Body);
    }

    [Fact]
    public void RequestRejected_SinDestinatario_DevuelveNull()
    {
        var alert = AlertMaterializer.RequestRejected(
            null,
            RequestId,
            TestData.SpecialtyId,
            "Sin cupos"
        );

        Assert.Null(alert);
    }

    [Fact]
    public void NewAppointment_AsociaCita()
    {
        var alert = AlertMaterializer.NewAppointment(
            Recipient,
            AppointmentId,
            TestData.SpecialtyId,
            "María",
            "Medicina General",
            DateTimeOffset.UtcNow
        );

        Assert.NotNull(alert);
        Assert.Equal(AlertType.NewAppointment, alert!.Type);
        Assert.Equal(AppointmentId, alert.RelatedAppointmentId);
    }

    [Fact]
    public void AppointmentRescheduled_EsWarningYAsociaCita()
    {
        var alert = AlertMaterializer.AppointmentRescheduled(
            Recipient,
            AppointmentId,
            "María",
            DateTimeOffset.UtcNow
        );

        Assert.NotNull(alert);
        Assert.Equal(AlertType.AppointmentRescheduled, alert!.Type);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
        Assert.Equal(AppointmentId, alert.RelatedAppointmentId);
    }

    [Fact]
    public void AppointmentCancelled_IncluyeRazon()
    {
        var alert = AlertMaterializer.AppointmentCancelled(
            Recipient,
            AppointmentId,
            "María",
            "Emergencia"
        );

        Assert.NotNull(alert);
        Assert.Equal(AlertType.AppointmentCancelled, alert!.Type);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
        Assert.Contains("Emergencia", alert!.Body);
    }

    [Fact]
    public void AppointmentCancelled_SinRazon_TerminaEnPunto()
    {
        var alert = AlertMaterializer.AppointmentCancelled(
            Recipient,
            AppointmentId,
            "María",
            null!
        );

        Assert.NotNull(alert);
        Assert.EndsWith(".", alert!.Body);
        Assert.DoesNotContain(":", alert.Body);
    }

    [Fact]
    public void PatientWaiting_InfoParaElProfesional()
    {
        var alert = AlertMaterializer.PatientWaiting(Recipient, AppointmentId, "María");

        Assert.NotNull(alert);
        Assert.Equal(AlertType.PatientWaiting, alert!.Type);
        Assert.Equal(AppointmentId, alert.RelatedAppointmentId);
    }

    [Fact]
    public void ProfessionalJoined_UsaTipoPatientJoined()
    {
        var alert = AlertMaterializer.ProfessionalJoined(Recipient, AppointmentId, "María");

        Assert.NotNull(alert);
        Assert.Equal(AlertType.PatientJoined, alert!.Type);
        Assert.Equal(AlertSeverity.Info, alert.Severity);
    }

    [Fact]
    public void ParticipantLeft_EsWarning()
    {
        var alert = AlertMaterializer.ParticipantLeft(Recipient, AppointmentId, "María");

        Assert.NotNull(alert);
        Assert.Equal(AlertType.ParticipantLeft, alert!.Type);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
    }

    [Fact]
    public void SessionEnded_EsInfo()
    {
        var alert = AlertMaterializer.SessionEnded(Recipient, AppointmentId, "María");

        Assert.NotNull(alert);
        Assert.Equal(AlertType.SessionEnded, alert!.Type);
        Assert.Equal(AlertSeverity.Info, alert.Severity);
    }

    [Fact]
    public void SinDestinatario_DevuelveNull()
    {
        Assert.Null(
            AlertMaterializer.NewRequest(
                null,
                RequestId,
                TestData.SpecialtyId,
                "María",
                "Medicina General"
            )
        );
        Assert.Null(
            AlertMaterializer.NewAppointment(
                null,
                AppointmentId,
                TestData.SpecialtyId,
                "María",
                "Medicina General",
                DateTimeOffset.UtcNow
            )
        );
        Assert.Null(
            AlertMaterializer.AppointmentRescheduled(
                null,
                AppointmentId,
                "María",
                DateTimeOffset.UtcNow
            )
        );
        Assert.Null(AlertMaterializer.AppointmentCancelled(null, AppointmentId, "María", "Razón"));
        Assert.Null(AlertMaterializer.PatientWaiting(null, AppointmentId, "María"));
        Assert.Null(AlertMaterializer.ProfessionalJoined(null, AppointmentId, "María"));
        Assert.Null(AlertMaterializer.ParticipantLeft(null, AppointmentId, "María"));
        Assert.Null(AlertMaterializer.SessionEnded(null, AppointmentId, "María"));
    }
}
