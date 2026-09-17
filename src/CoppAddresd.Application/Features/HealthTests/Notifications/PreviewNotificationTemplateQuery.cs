using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums.HealthTests;
using MediatR;

namespace CoppAddresd.Application.Features.HealthTests.Notifications;

// --- Previsualización real de una plantilla ---

/// <summary>
/// Renderiza una plantilla con los datos REALES de una alerta (o, si no se
/// indica alerta, con la alerta más reciente que corresponda al alcance). La
/// vista previa no envía ni registra nada: solo devuelve el cuerpo resuelto, el
/// destinatario y si el canal está disponible para ese paciente.
/// </summary>
public record PreviewNotificationTemplateQuery(
    Guid TemplateId,
    Guid? AlertId = null,
    NotificationChannel? Channel = null,
    string? BodyOverride = null,
    NotificationLanguage Language = NotificationLanguage.es,
    Guid? ProfessionalId = null
) : IRequest<NotificationTemplatePreviewDto?>;

public sealed class PreviewNotificationTemplateQueryHandler(
    IHealthTestNotificationRepository notificationRepository,
    IHealthTestRepository healthTestRepository,
    IHealthTestTemplateRenderer renderer
) : IRequestHandler<PreviewNotificationTemplateQuery, NotificationTemplatePreviewDto?>
{
    public async Task<NotificationTemplatePreviewDto?> Handle(
        PreviewNotificationTemplateQuery request,
        CancellationToken ct
    )
    {
        var template = await notificationRepository.GetTemplateByIdAsync(request.TemplateId, false, ct);
        if (template is null)
        {
            return null;
        }

        var channel = request.Channel ?? template.Channel;
        var alert = await ResolveAlertAsync(request, ct);

        // Si falta la traducción al inglés se usa el español y se avisa a la UI.
        var usedFallbackLanguage =
            request.Language == NotificationLanguage.en
            && string.IsNullOrWhiteSpace(template.BodyTemplateEn);
        var templateName =
            request.Language == NotificationLanguage.en
                ? (template.NameEn ?? template.NameEs)
                : template.NameEs;
        var templateBody = NotifyAlertsCommandHandler.TemplateBody(template, request.Language);

        var body = request.BodyOverride is { Length: > 0 } ? request.BodyOverride : templateBody;

        if (alert is null)
        {
            // Sin alerta disponible: se muestra el cuerpo sin datos dinámicos.
            return new NotificationTemplatePreviewDto(
                template.Id,
                templateName,
                channel,
                request.Language,
                null,
                null,
                body,
                renderer.Render(body, new HealthTestNotificationRenderContext()),
                null,
                false,
                "No hay alertas disponibles para generar la vista previa.",
                usedFallbackLanguage,
                renderer.ExtractPlaceholders(body)
            );
        }

        var patient = (await notificationRepository.GetPatientsByIdsAsync([alert.PatientId], ct))
            .GetValueOrDefault(alert.PatientId);

        var context = BuildContext(alert, patient, request.Language);
        var recipient = NotificationPreviewSupport.ResolveRecipient(channel, patient);
        var placeholders = renderer.ExtractPlaceholders(body);
        var missing = placeholders
            .Where(p => !ContextHasValue(p, context))
            .ToList();

        return new NotificationTemplatePreviewDto(
            template.Id,
            templateName,
            channel,
            request.Language,
            alert.Id,
            alert.PatientId,
            body,
            renderer.Render(body, context),
            recipient.Value,
            recipient.IsReachable,
            recipient.SkipReason,
            usedFallbackLanguage,
            missing
        );
    }

    private async Task<Domain.Entities.HealthTests.HealthTestAlert?> ResolveAlertAsync(
        PreviewNotificationTemplateQuery request,
        CancellationToken ct
    )
    {
        if (request.AlertId is { } alertId && alertId != Guid.Empty)
        {
            return (await notificationRepository.ListAlertsByIdsAsync([alertId], ct)).FirstOrDefault();
        }

        // Sin alerta explícita: la más reciente que tenga resultado asociado
        // (así la vista previa muestra indicador y valor reales); si ninguna lo
        // tiene, se usa la más reciente disponible.
        var (alerts, _) = await healthTestRepository.ListAlertsAsync(
            null,
            null,
            null,
            1,
            30,
            ct
        );
        return alerts.FirstOrDefault(a => a.ResultId is not null) ?? alerts.FirstOrDefault();
    }

    internal static HealthTestNotificationRenderContext BuildContext(
        Domain.Entities.HealthTests.HealthTestAlert alert,
        PatientProfile? patient,
        NotificationLanguage language = NotificationLanguage.es
    ) =>
        new(
            patient is null ? "Paciente" : $"{patient.FirstName} {patient.LastName}".Trim(),
            patient?.DocumentNumber,
            alert.Rule?.Name,
            alert.Result?.Label ?? alert.Rule?.Name,
            alert.Result is null ? null : alert.Result.Value.ToString("0.##"),
            null,
            alert.Severity,
            null,
            null,
            alert.CreatedAt,
            language
        );

    /// <summary>¿El contexto tiene dato para esa clave? (para avisar de vacíos).</summary>
    private static bool ContextHasValue(
        string placeholder,
        HealthTestNotificationRenderContext context
    ) =>
        placeholder switch
        {
            "paciente" => !string.IsNullOrWhiteSpace(context.PatientName),
            "documento" => !string.IsNullOrWhiteSpace(context.PatientDocument),
            "test" => !string.IsNullOrWhiteSpace(context.TestName),
            "indicador" => !string.IsNullOrWhiteSpace(context.IndicatorName),
            "valor" => !string.IsNullOrWhiteSpace(context.Value),
            "umbral" => !string.IsNullOrWhiteSpace(context.Threshold),
            "severidad" => context.Severity is not null,
            "accion" => !string.IsNullOrWhiteSpace(context.RecommendedAction),
            "profesional" => !string.IsNullOrWhiteSpace(context.ProfessionalName),
            "fecha" => context.Date is not null,
            _ => true,
        };
}
