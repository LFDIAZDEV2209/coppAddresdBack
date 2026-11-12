using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.WhatsApp.Messaging;
using CoppAddresd.Domain.Entities.WhatsApp;
using CoppAddresd.WhatsApp.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.WhatsApp.Messaging;

public sealed class WhatsAppPatientResolver(WhatsAppDbContext db, ISender mediator,
    IWhatsAppMessageSender sender, TimeProvider clock)
{
    public async Task<PatientLink?> ResolveAsync(string phone, DateTimeOffset inboundAt, CancellationToken ct)
    {
        var waId = PatientPhone.Normalize(phone);
        if (waId is null) return null;
        var link = await db.PatientLinks.SingleOrDefaultAsync(x => x.WaId == waId, ct);
        var now = clock.GetUtcNow();
        if (link is null)
        {
            var patient = await mediator.Send(new FindActivePatientByPhoneQuery(waId), ct);
            if (patient is null) return null;
            var id = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO whatsapp.patient_links (id, patient_id, user_id, wa_id, first_name, language, opt_out, created_at, updated_at)
                VALUES ({id}, {patient.PatientId}, {patient.UserId}, {waId}, {patient.FirstName}, 'es', false, {now}, {now})
                ON CONFLICT DO NOTHING
                """, ct);
            link = await db.PatientLinks.SingleOrDefaultAsync(x => x.WaId == waId, ct);
            if (link is null) return null;
        }
        var conversationId = Guid.NewGuid();
        var threadId = "whatsapp-" + conversationId.ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO whatsapp.conversations (id, patient_id, thread_id, language, last_inbound_at)
            VALUES ({conversationId}, {link.PatientId}, {threadId}, {link.Language}, {inboundAt})
            ON CONFLICT (patient_id) DO UPDATE
                SET last_inbound_at = GREATEST(whatsapp.conversations.last_inbound_at, EXCLUDED.last_inbound_at)
            """, ct);
        return link;
    }

    public Task<WhatsAppSendResult> GuideUnknownAsync(string waId, DateTimeOffset inboundAt, CancellationToken ct) =>
        sender.SendTextAsync(waId, "Para conversar con tu asistente, regístrate en la app ANTARES con este número de teléfono.", inboundAt, ct);
}
