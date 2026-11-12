using CoppAddresd.Application.Features.Chat;
using CoppAddresd.Application.WhatsApp.Messaging;
using CoppAddresd.Domain.Entities.WhatsApp;
using CoppAddresd.WhatsApp.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.WhatsApp.Messaging;

public sealed class WhatsAppInboundProcessor(
    WhatsAppDbContext db,
    WhatsAppPatientResolver resolver,
    ISender mediator,
    IWhatsAppMessageSender sender,
    TimeProvider clock,
    ILogger<WhatsAppInboundProcessor> logger,
    CoppAddresd.Application.WhatsApp.IWhatsAppReminderHooks? reminders = null,
    CoppAddresd.WhatsApp.Binding.WhatsAppBindingFlow? binding = null
)
{
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var lease = now.AddMinutes(5);
        // Claim atómico con transacción implícita corta. Ningún lock de fila vive durante el HTTP.
        // Un evento anterior pendiente bloquea solo su número: mantiene orden y memoria entre réplicas.
        var claimed = await db
            .RawEvents.FromSqlInterpolated(
                $"""
                UPDATE whatsapp.raw_events SET lease_until = {lease}, attempts = attempts + 1
                WHERE id = (
                    SELECT e.id FROM whatsapp.raw_events e
                    WHERE e.processed_at IS NULL AND e.next_attempt_at <= {now}
                      AND (e.lease_until IS NULL OR e.lease_until < {now})
                      AND NOT EXISTS (SELECT 1 FROM whatsapp.raw_events p
                        WHERE p.wa_id = e.wa_id AND p.processed_at IS NULL
                          AND (p.received_at, p.id) < (e.received_at, e.id))
                    ORDER BY e.received_at, e.id LIMIT 1 FOR UPDATE OF e SKIP LOCKED
                ) RETURNING *
                """
            )
            .ToListAsync(ct);
        if (claimed.Count == 0)
            return false;
        var input = claimed[0];
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        // Indicador de escritura (tres puntos): se marca el mensaje como leído
        // y se refresca cada 20 s mientras se prepara la respuesta. Meta lo
        // descarta a los 25 s o al responder; es cosmético y nunca rompe el flujo.
        var typingCts =
            input.Reply is null && !string.IsNullOrEmpty(input.Wamid)
                ? StartTypingIndicator(input.Wamid, deadline.Token)
                : null;
        try
        {
            if (input.Reply is null)
                await PrepareReplyAsync(input, deadline.Token);
            var fragments = MessagePolicy.Split(
                MessagePolicy.NormalizeForWhatsApp(input.Reply ?? "")
            );
            for (var i = input.SentFragments; i < fragments.Count; i++)
            {
                var result = await sender.SendTextAsync(
                    input.WaId,
                    fragments[i],
                    input.MessageAt,
                    deadline.Token
                );
                if (!result.Succeeded)
                {
                    await FailAsync(
                        input,
                        result.ErrorCode ?? "send_failed",
                        result.Failure == WhatsAppFailureKind.Transient,
                        ct
                    );
                    return true;
                }
                input.SentFragments = i + 1;
                await db.SaveChangesAsync(deadline.Token);
            }
            input.ProcessedAt = clock.GetUtcNow();
            input.LeaseUntil = null;
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Procesamiento WhatsApp fallido para {Phone}: {Cause}",
                MessagePolicy.MaskPhone(input.WaId),
                ex.GetType().Name
            );
            await FailAsync(
                input,
                ex is OperationCanceledException ? "processing_timeout" : "processing_failed",
                true,
                ct
            );
        }
        finally
        {
            // Corta el refresco del indicador en TODAS las salidas del turno.
            typingCts?.Cancel();
            typingCts?.Dispose();
        }
        return true;
    }

    private async Task PrepareReplyAsync(WhatsAppRawEvent input, CancellationToken ct)
    {
        var link = await resolver.ResolveAsync(input.WaId, input.MessageAt, ct);
        if (link is null)
        {
            if (binding is not null)
                await binding.HandleAsync(input, ct);
            else
                input.Reply = Binding.WhatsAppBindingFlow.Guide;
        }
        else
        {
            if (reminders is not null)
                await reminders.CancelForPatientAsync(link.PatientId, ct);
            var conversation = await db.Conversations.SingleAsync(
                x => x.PatientId == link.PatientId,
                ct
            );
            var language = TurnLanguage.Detect(input.Text, conversation.Language);
            conversation.Language = link.Language = language;
            link.UpdatedAt = clock.GetUtcNow();
            var optOut = TurnLanguage.OptOutCommand(input.Text);
            if (optOut is not null)
            {
                link.OptOut = optOut.Value;
                input.Reply = (optOut.Value, language) switch
                {
                    (true, "en") => "Reminders paused. Send START to resume.",
                    (false, "en") => "Reminders resumed. Send STOP to pause.",
                    (true, _) => "Recordatorios pausados. Envía INICIAR para reactivarlos.",
                    _ => "Recordatorios reactivados. Envía SALIR para pausarlos.",
                };
            }
            else if (string.IsNullOrWhiteSpace(input.Text))
                input.Reply =
                    language == "en"
                        ? "Please send a text message to chat with your assistant."
                        : "Envía un mensaje de texto para conversar con tu asistente.";
            else
            {
                await db.SaveChangesAsync(ct);
                try
                {
                    using var agentDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    agentDeadline.CancelAfter(TimeSpan.FromSeconds(90));
                    // El canal WhatsApp usa su propio perfil de agente ("whatsapp" en el
                    // ai-service): sin promesas de citas ni CTA de agenda y con formato
                    // de mensajería; el chat de la app móvil conserva el perfil base.
                    var result = await mediator.Send(
                        new ChatCommand(
                            input.Text,
                            Agent: "whatsapp",
                            ThreadId: conversation.ThreadId,
                            UserId: link.UserId?.ToString(),
                            Language: language,
                            IncludeControlContext: false,
                            PatientId: link.PatientId.ToString()
                        ),
                        agentDeadline.Token
                    );
                    conversation.ThreadId = result.ThreadId;
                    input.Reply = string.IsNullOrWhiteSpace(result.Reply)
                        ? SafeFailure(language)
                        : result.Reply;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    input.ErrorCode = "agent_" + ex.GetType().Name;
                    logger.LogWarning(
                        "Agente WhatsApp no disponible para {Phone}: {Cause}",
                        MessagePolicy.MaskPhone(input.WaId),
                        ex.GetType().Name
                    );
                    input.Reply = SafeFailure(language);
                }
            }
        }
        // Guardar antes del envío permite replay sin volver a invocar el LLM.
        await db.SaveChangesAsync(ct);
    }

    private Task FailAsync(WhatsAppRawEvent input, string code, bool retry, CancellationToken ct)
    {
        input.ErrorCode = code;
        input.LeaseUntil = null;
        if (!retry || input.Attempts >= 5)
            input.ProcessedAt = clock.GetUtcNow();
        else
            input.NextAttemptAt = clock
                .GetUtcNow()
                .AddSeconds(Math.Min(60, 5 * Math.Pow(2, input.Attempts - 1)));
        return db.SaveChangesAsync(ct);
    }

    private static string SafeFailure(string language) =>
        language == "en"
            ? "Your assistant is temporarily unavailable. Please try again in a few minutes."
            : "Tu asistente no está disponible en este momento. Inténtalo de nuevo en unos minutos.";

    /// <summary>
    /// Bucle del indicador de escritura: primera marca inmediata y refresco
    /// cada 20 s hasta que el proceso se complete o se cancele. Meta descarta
    /// el indicador a los 25 s, así que el refresco mantiene la animación
    /// durante todo el turno del agente.
    /// </summary>
    private CancellationTokenSource StartTypingIndicator(string wamid, CancellationToken ct)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(
            async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        await sender.ShowTypingAsync(wamid, cts.Token);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Indicador de escritura fallido (cosmético)");
                    }
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(20), cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            },
            CancellationToken.None
        );
        return cts;
    }
}
