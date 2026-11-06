using System.Globalization;
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
    IHealthTestTemplateRenderer renderer,
    IContentTranslationService? translations = null
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

        var english = request.Language == NotificationLanguage.en;
        var missing = new List<string>();
        if (english && string.IsNullOrWhiteSpace(template.NameEn)) missing.Add(template.NameEs);
        if (english && string.IsNullOrWhiteSpace(template.BodyTemplateEn) && string.IsNullOrWhiteSpace(request.BodyOverride)) missing.Add(template.BodyTemplateEs);
        var source = !string.IsNullOrWhiteSpace(request.BodyOverride) ? request.BodyOverride
            : english && !string.IsNullOrWhiteSpace(template.BodyTemplateEn) ? template.BodyTemplateEn : template.BodyTemplateEs;
        if (english && alert is not null) missing.AddRange(HealthTestTemplateLocalization.AuthoredLabels(alert, source));
        var localized = await HealthTestTemplateLocalization.PrepareAsync(missing, request.Language, translations, ct);
        var templateName = english ? (!string.IsNullOrWhiteSpace(template.NameEn) ? template.NameEn! : localized[template.NameEs]) : template.NameEs;
        var body = !string.IsNullOrWhiteSpace(request.BodyOverride) ? request.BodyOverride
            : english ? (!string.IsNullOrWhiteSpace(template.BodyTemplateEn) ? template.BodyTemplateEn! : localized[template.BodyTemplateEs]) : template.BodyTemplateEs;
        const bool usedFallbackLanguage = false;

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
                renderer.Render(body, new HealthTestNotificationRenderContext(Language: request.Language)),
                null,
                false,
                "No hay alertas disponibles para generar la vista previa.",
                usedFallbackLanguage,
                renderer.ExtractPlaceholders(body)
            );
        }

        var patient = (await notificationRepository.GetPatientsByIdsAsync([alert.PatientId], ct))
            .GetValueOrDefault(alert.PatientId);

        var context = BuildContext(alert, patient, request.Language, localized);
        var recipient = NotificationPreviewSupport.ResolveRecipient(channel, patient);
        var placeholders = renderer.ExtractPlaceholders(body);
        var missingPlaceholders = placeholders
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
            missingPlaceholders
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
        NotificationLanguage language = NotificationLanguage.es,
        IReadOnlyDictionary<string, string>? localized = null
    ) =>
        new(
            patient is null ? (language == NotificationLanguage.en ? "Patient" : "Paciente") : $"{patient.FirstName} {patient.LastName}".Trim(),
            patient?.DocumentNumber,
            HealthTestTemplateLocalization.DisplayLabel(alert.Rule?.Name, language, localized),
            HealthTestTemplateLocalization.DisplayLabel(alert.Result?.Label ?? alert.Rule?.Name, language, localized),
            alert.Result is null ? null : alert.Result.Value.ToString("0.##", CultureInfo.InvariantCulture),
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
