using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.HealthTests.Notifications;

/// <summary>
/// Orquesta el envío (o previsualización) de notificaciones a los pacientes de
/// una o varias alertas. Resuelve plantilla, destinatario y canal, registra cada
/// entrega en <see cref="HealthTestNotification"/> y devuelve el resultado por
/// paciente. Un contacto ausente se marca como <c>skipped</c> y nunca bloquea el
/// resto del envío.
/// </summary>
public record NotifyAlertsCommand(NotifyAlertsRequest Request, Guid? ActorId = null)
    : IRequest<NotifyAlertsResultDto>;

public sealed class NotifyAlertsCommandHandler(
    IHealthTestNotificationRepository notificationRepository,
    IHealthTestTemplateRenderer renderer,
    ISmsSender smsSender,
    ICommunityMessageSender communitySender,
    ILogger<NotifyAlertsCommandHandler> logger
) : IRequestHandler<NotifyAlertsCommand, NotifyAlertsResultDto>
{
    public async Task<NotifyAlertsResultDto> Handle(
        NotifyAlertsCommand command,
        CancellationToken ct
    )
    {
        var request = command.Request;
        var alertIds = (request.AlertIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var channels = (request.Channels ?? []).Distinct().ToList();

        if (alertIds.Count == 0 || channels.Count == 0)
        {
            return new NotifyAlertsResultDto(0, 0, 0, 0, request.Preview, []);
        }

        var alerts = await notificationRepository.ListAlertsByIdsAsync(alertIds, ct);
        var patients = await notificationRepository.GetPatientsByIdsAsync(
            alerts
                .Where(a => a.PatientId != Guid.Empty)
                .Select(a => a.PatientId)
                .Distinct()
                .ToList(),
            ct
        );

        var explicitTemplate = request.TemplateId is { } templateId
            ? await notificationRepository.GetTemplateByIdAsync(templateId, false, ct)
            : null;

        var candidates = await LoadCandidateTemplatesAsync(channels, ct);

        var items = new List<NotifyAlertItemResultDto>();
        var pending = new List<HealthTestNotification>();

        foreach (var alert in alerts)
        {
            var patient = alert.Patient
                ?? (patients.TryGetValue(alert.PatientId, out var p) ? p : null);

            foreach (var channel in channels)
            {
                var template = explicitTemplate ?? PickTemplate(candidates, channel, alert, patient);
                var context = BuildContext(alert, patient, request.Language);
                var body = ResolveBody(
                    request.BodyOverride,
                    template,
                    alert,
                    context,
                    request.Language
                );
                var recipient = ResolveRecipient(channel, patient);

                if (!recipient.IsReachable)
                {
                    items.Add(
                        new NotifyAlertItemResultDto(
                            alert.Id,
                            patient?.Id,
                            PatientName(patient),
                            channel,
                            request.Language,
                            NotificationStatus.skipped,
                            recipient.SkipReason,
                            body,
                            recipient.Value
                        )
                    );
                    continue;
                }

                if (request.Preview)
                {
                    items.Add(
                        new NotifyAlertItemResultDto(
                            alert.Id,
                            patient?.Id,
                            PatientName(patient),
                            channel,
                            request.Language,
                            NotificationStatus.queued,
                            null,
                            body,
                            recipient.Value
                        )
                    );
                    continue;
                }

                var (status, provider, providerMessageId, error) = await DispatchAsync(
                    channel,
                    recipient.Value,
                    body,
                    patient,
                    command.ActorId,
                    ct
                );

                var notification = new HealthTestNotification
                {
                    Id = Guid.NewGuid(),
                    AlertId = alert.Id,
                    PatientId = patient?.Id,
                    Channel = channel,
                    Language = request.Language,
                    TemplateId = template?.Id,
                    Recipient = recipient.Value,
                    RenderedBody = body,
                    Status = status,
                    Provider = provider,
                    ProviderMessageId = providerMessageId,
                    Error = error,
                    CreatedBy = command.ActorId,
                    CreatedAt = DateTime.UtcNow,
                    SentAt = status == NotificationStatus.sent ? DateTime.UtcNow : null,
                };
                pending.Add(notification);

                items.Add(
                    new NotifyAlertItemResultDto(
                        alert.Id,
                        patient?.Id,
                        PatientName(patient),
                        channel,
                        request.Language,
                        status,
                        error,
                        body,
                        recipient.Value
                    )
                );
            }
        }

        if (pending.Count > 0)
        {
            await notificationRepository.AddNotificationsAsync(pending, ct);
        }

        return new NotifyAlertsResultDto(
            alerts.Count * channels.Count,
            items.Count(i => i.Status == NotificationStatus.sent),
            items.Count(i => i.Status == NotificationStatus.skipped),
            items.Count(i => i.Status == NotificationStatus.failed),
            request.Preview,
            items
        );
    }

    private async Task<(NotificationStatus Status, string Provider, string? MessageId, string? Error)> DispatchAsync(
        NotificationChannel channel,
        string recipient,
        string body,
        PatientProfile? patient,
        Guid? actorId,
        CancellationToken ct
    )
    {
        try
        {
            if (channel == NotificationChannel.sms)
            {
                var result = await smsSender.SendAsync(recipient, body, ct);
                return result.Success
                    ? (NotificationStatus.sent, smsSender.Provider, result.ProviderMessageId, null)
                    : (NotificationStatus.failed, smsSender.Provider, null, result.Error);
            }

            var community = await communitySender.SendDirectMessageAsync(
                patient?.UserId,
                body,
                actorId,
                PatientName(patient),
                ct
            );
            if (community.Success)
            {
                return (
                    NotificationStatus.sent,
                    communitySender.Provider,
                    community.ProviderMessageId,
                    null
                );
            }

            if (!string.IsNullOrWhiteSpace(community.SkipReason))
            {
                return (
                    NotificationStatus.skipped,
                    communitySender.Provider,
                    null,
                    community.SkipReason
                );
            }

            return (NotificationStatus.failed, communitySender.Provider, null, community.Error);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Fallo al notificar por {Channel} al destinatario {Recipient}.",
                channel,
                recipient
            );
            var provider = channel == NotificationChannel.sms ? smsSender.Provider : communitySender.Provider;
            return (NotificationStatus.failed, provider, null, ex.Message);
        }
    }

    private async Task<IReadOnlyList<HealthTestNotificationTemplate>> LoadCandidateTemplatesAsync(
        IReadOnlyList<NotificationChannel> channels,
        CancellationToken ct
    )
    {
        var result = new List<HealthTestNotificationTemplate>();
        foreach (var channel in channels)
        {
            var templates = await notificationRepository.ListTemplatesAsync(
                channel,
                null,
                true,
                1,
                100,
                ct
            );
            result.AddRange(templates);
        }

        return result;
    }

    private static HealthTestNotificationTemplate? PickTemplate(
        IReadOnlyList<HealthTestNotificationTemplate> candidates,
        NotificationChannel channel,
        HealthTestAlert alert,
        PatientProfile? patient
    )
    {
        var channelTemplates = candidates.Where(t => t.Channel == channel).ToList();
        if (channelTemplates.Count == 0)
        {
            return null;
        }

        var indicator = alert.Result?.Code;

        return channelTemplates
            .OrderByDescending(t => Score(t, alert.Severity, indicator))
            .ThenByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .FirstOrDefault();
    }

    private static int Score(
        HealthTestNotificationTemplate template,
        HealthTestSeverity severity,
        string? indicatorCode
    )
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(indicatorCode)
            && string.Equals(template.IndicatorCode, indicatorCode, StringComparison.OrdinalIgnoreCase))
        {
            score += 4;
        }

        if (template.Severity == severity)
        {
            score += 2;
        }

        if (template.Severity is null)
        {
            score += 1;
        }

        return score;
    }

    private string ResolveBody(
        string? bodyOverride,
        HealthTestNotificationTemplate? template,
        HealthTestAlert alert,
        HealthTestNotificationRenderContext context,
        NotificationLanguage language
    )
    {
        if (!string.IsNullOrWhiteSpace(bodyOverride))
        {
            return renderer.Render(bodyOverride, context);
        }

        if (template is not null)
        {
            return renderer.Render(TemplateBody(template, language), context);
        }

        if (!string.IsNullOrWhiteSpace(alert.Rule?.MessageTemplate))
        {
            return renderer.Render(alert.Rule!.MessageTemplate, context);
        }

        return !string.IsNullOrWhiteSpace(alert.Body) ? alert.Body! : alert.Title;
    }

    /// <summary>
    /// Cuerpo de la plantilla para el idioma pedido. Si falta la traducción al
    /// inglés se usa el español (la UI avisa de la traducción pendiente).
    /// </summary>
    internal static string TemplateBody(
        HealthTestNotificationTemplate template,
        NotificationLanguage language
    ) =>
        language == NotificationLanguage.en
            ? (
                string.IsNullOrWhiteSpace(template.BodyTemplateEn)
                    ? template.BodyTemplateEs
                    : template.BodyTemplateEn!
            )
            : template.BodyTemplateEs;

    private static HealthTestNotificationRenderContext BuildContext(
        HealthTestAlert alert,
        PatientProfile? patient,
        NotificationLanguage language = NotificationLanguage.es
    ) =>
        new(
            PatientName(patient) ?? "Paciente",
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

    private static string? PatientName(PatientProfile? patient) =>
        patient is null ? null : $"{patient.FirstName} {patient.LastName}".Trim();

    private static (bool IsReachable, string Value, string? SkipReason) ResolveRecipient(
        NotificationChannel channel,
        PatientProfile? patient
    ) => NotificationPreviewSupport.ResolveRecipient(channel, patient);
}

/// <summary>
/// Envía una notificación de prueba de una plantilla (botón "Enviar prueba" del
/// Template Studio). Usa un paciente real si se indica; si no, usa un teléfono o
/// destinatario de prueba. Queda registrada en el log con <c>AlertId = null</c>.
/// </summary>
public record SendTestNotificationCommand(
    Guid TemplateId,
    SendTestNotificationRequest Request,
    Guid? ActorId = null
) : IRequest<NotifyAlertItemResultDto?>;

public sealed class SendTestNotificationCommandHandler(
    IHealthTestNotificationRepository notificationRepository,
    IHealthTestTemplateRenderer renderer,
    ISmsSender smsSender,
    ICommunityMessageSender communitySender
) : IRequestHandler<SendTestNotificationCommand, NotifyAlertItemResultDto?>
{
    public async Task<NotifyAlertItemResultDto?> Handle(
        SendTestNotificationCommand command,
        CancellationToken ct
    )
    {
        var template = await notificationRepository.GetTemplateByIdAsync(
            command.TemplateId,
            false,
            ct
        );
        if (template is null)
        {
            return null;
        }

        var request = command.Request;
        PatientProfile? patient = null;
        if (request.PatientId is { } patientId && patientId != Guid.Empty)
        {
            var patients = await notificationRepository.GetPatientsByIdsAsync([patientId], ct);
            patients.TryGetValue(patientId, out patient);
        }

        var context = new HealthTestNotificationRenderContext(
            patient is null ? "Paciente de prueba" : $"{patient.FirstName} {patient.LastName}".Trim(),
            patient?.DocumentNumber,
            "Test de ejemplo",
            template.IndicatorCode ?? "Indicador de ejemplo",
            "3.5",
            null,
            template.Severity,
            null,
            null,
            DateTime.UtcNow,
            request.Language
        );
        var body = renderer.Render(
            request.BodyOverride ?? NotifyAlertsCommandHandler.TemplateBody(template, request.Language),
            context
        );

        string recipient;
        if (request.Channel == NotificationChannel.sms)
        {
            recipient = string.IsNullOrWhiteSpace(request.PhoneNumber)
                ? "+10000000000"
                : request.PhoneNumber!;
        }
        else
        {
            recipient = patient?.UserId?.ToString() ?? "prueba";
        }

        NotificationStatus status;
        string provider;
        string? providerMessageId;
        string? error;

        try
        {
            if (request.Channel == NotificationChannel.sms)
            {
                var result = await smsSender.SendAsync(recipient, body, ct);
                status = result.Success ? NotificationStatus.sent : NotificationStatus.failed;
                provider = smsSender.Provider;
                providerMessageId = result.ProviderMessageId;
                error = result.Error;
            }
            else
            {
                var result = await communitySender.SendDirectMessageAsync(
                    patient?.UserId,
                    body,
                    command.ActorId,
                    patient is null
                        ? null
                        : $"{patient.FirstName} {patient.LastName}".Trim(),
                    ct
                );
                status = result.Success
                    ? NotificationStatus.sent
                    : string.IsNullOrWhiteSpace(result.SkipReason)
                        ? NotificationStatus.failed
                        : NotificationStatus.skipped;
                provider = communitySender.Provider;
                providerMessageId = result.ProviderMessageId;
                error = result.SkipReason ?? result.Error;
            }
        }
        catch (Exception ex)
        {
            status = NotificationStatus.failed;
            provider = request.Channel == NotificationChannel.sms
                ? smsSender.Provider
                : communitySender.Provider;
            providerMessageId = null;
            error = ex.Message;
        }

        var notification = new HealthTestNotification
        {
            Id = Guid.NewGuid(),
            AlertId = null,
            PatientId = patient?.Id,
            Channel = request.Channel,
            Language = request.Language,
            TemplateId = template.Id,
            Recipient = recipient,
            RenderedBody = body,
            Status = status,
            Provider = provider,
            ProviderMessageId = providerMessageId,
            Error = error,
            CreatedBy = command.ActorId,
            CreatedAt = DateTime.UtcNow,
            SentAt = status == NotificationStatus.sent ? DateTime.UtcNow : null,
        };
        await notificationRepository.AddNotificationsAsync([notification], ct);

        return new NotifyAlertItemResultDto(
            null,
            patient?.Id,
            patient is null ? null : $"{patient.FirstName} {patient.LastName}".Trim(),
            request.Channel,
            request.Language,
            status,
            error,
            body,
            recipient
        );
    }
}
