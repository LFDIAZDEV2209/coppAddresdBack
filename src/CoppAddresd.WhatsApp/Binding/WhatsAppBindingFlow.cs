using System.Text.RegularExpressions;
using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.WhatsApp.Binding;
using CoppAddresd.Domain.Entities.WhatsApp;
using CoppAddresd.WhatsApp.Messaging;
using CoppAddresd.WhatsApp.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.WhatsApp.Binding;

public sealed class WhatsAppBindingFlow(WhatsAppDbContext db, ISender mediator, IPatientBindingQuery patients,
    IWhatsAppOtpClient auth, TimeProvider clock)
{
    public const string Guide = "Puedes vincular este número sin contraseña: envía VINCULAR y después tu número de documento. Te enviaremos un código por SMS al teléfono registrado. También puedes registrarte en la app ANTARES. Nunca envíes contraseñas.";
    private const string Limit = "Alcanzaste el límite de 2 envíos por hora. Intenta cuando se libere la ventana de una hora.";
    private const string Unavailable = "No pudimos completar la vinculación. Inténtalo más tarde enviando VINCULAR o usa la app ANTARES.";

    public async Task HandleAsync(WhatsAppRawEvent input, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO whatsapp.binding_sessions (id, wa_id, state, attempts, otp_sends, otp_sent_at, expires_at)
            VALUES ({id}, {input.WaId}, 'Ended', 0, 0, ARRAY[]::timestamptz[], {now}) ON CONFLICT (wa_id) DO NOTHING
            """, ct);
        var owner = Guid.NewGuid();
        var lease = now.AddMinutes(2);
        var claimed = await db.BindingSessions.Where(x => x.WaId == input.WaId && (x.LeaseUntil == null || x.LeaseUntil < now))
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.LeaseOwner, owner).SetProperty(x => x.LeaseUntil, lease), ct);
        if (claimed == 0)
        {
            input.Reply = "Estamos procesando la vinculación. Espera un momento y vuelve a enviar tu mensaje.";
            Scrub(input);
            await db.SaveChangesAsync(ct);
            return;
        }
        var session = await db.BindingSessions.SingleAsync(x => x.WaId == input.WaId, ct);
        if (session.LastEventId == input.Id && session.LastReply is not null)
        {
            await FinishAsync(session, input, session.LastReply, ct);
            return;
        }
        var text = input.Text.Trim();
        var command = TurnLanguage.Normalize(text);
        if (command is "cancelar" or "cancel")
        {
            session.State = BindingState.Ended;
            await FinishAsync(session, input, "Vinculación cancelada. Puedes registrarte en la app ANTARES o enviar VINCULAR cuando quieras.", ct);
            return;
        }
        if (command is "vincular" or "link")
        {
            if (!session.CanSend(now)) { await FinishAsync(session, input, Limit, ct); return; }
            session.Begin(now);
            await FinishAsync(session, input, "Escribe únicamente tu número de documento. Nunca envíes tu contraseña. Puedes enviar CANCELAR para terminar.", ct);
            return;
        }
        if (session.IsActive && session.IsExpired(now))
        {
            session.State = BindingState.Expired;
            await FinishAsync(session, input, "La vinculación expiró a los 10 minutos. Envía VINCULAR para comenzar de nuevo.", ct);
            return;
        }
        if (session.State is BindingState.Sending or BindingState.Verifying)
        {
            // Un proceso pudo morir después de que Auth aceptara el envío/check. Nunca repetirlo a ciegas.
            session.State = BindingState.Ended;
            await FinishAsync(session, input, Unavailable, ct);
            return;
        }
        if (session.State == BindingState.Expired || session.State == BindingState.Ended && session.Attempts >= 3)
        {
            await FinishAsync(session, input, "La vinculación terminó por expiración o intentos agotados. Envía VINCULAR para comenzar de nuevo.", ct);
            return;
        }
        if (command.Contains("contrasena") || command.Contains("password"))
        {
            await FinishAsync(session, input, "No envíes contraseñas. Para vincularte usa solo tu documento y el código SMS solicitado, o la app ANTARES.", ct);
            return;
        }
        if (session.State == BindingState.AwaitingCode)
        {
            if (command is "reenviar" or "resend")
                await SendAsync(session, input, await CurrentPatientAsync(session, ct), ct);
            else await VerifyAsync(session, input, text, ct);
            return;
        }
        // Se admite el documento directamente, o después de VINCULAR. Texto libre no es un documento.
        if (!Regex.IsMatch(text, @"\A[A-Za-z0-9.-]{4,50}\z", RegexOptions.CultureInvariant) || !text.Any(char.IsAsciiDigit))
        {
            await FinishAsync(session, input, Guide, ct);
            return;
        }
        if (!session.CanSend(now)) { await FinishAsync(session, input, Limit, ct); return; }
        if (!session.IsActive) session.Begin(now);
        var patient = await mediator.Send(new FindActivePatientForBindingQuery(text), ct);
        await SendAsync(session, input, patient, ct);
    }

    private Task<PatientBindingIdentity?> CurrentPatientAsync(BindingSession session, CancellationToken ct) => session.PatientId is { } id
        ? patients.FindByIdAsync(id, ct) : Task.FromResult<PatientBindingIdentity?>(null);

    private async Task SendAsync(BindingSession session, WhatsAppRawEvent input, PatientBindingIdentity? patient, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (!session.CanSend(now)) { await FinishAsync(session, input, Limit, ct); return; }
        var phone = patient?.PhoneNumber is { } number ? PatientPhone.Normalize(number, patient.PhoneCountryCode) : null;
        if (patient is null || phone is null)
        {
            session.State = BindingState.Ended;
            await FinishAsync(session, input, Guide, ct);
            return;
        }
        var lookup = await auth.LookupAsync(patient.DocumentNumber, ct);
        if (lookup.Outcome != OtpOutcome.Approved || lookup.Phone?.PatientId != patient.PatientId)
        {
            session.State = BindingState.Ended;
            await FinishAsync(session, input, lookup.Outcome == OtpOutcome.RateLimited ? AuthLimit : Guide, ct);
            return;
        }
        session.PatientId = patient.PatientId;
        session.ContactId = "phone";
        if (session.IsExpired(clock.GetUtcNow()))
        {
            session.State = BindingState.Expired;
            await FinishAsync(session, input, "La vinculación expiró a los 10 minutos. Envía VINCULAR para comenzar de nuevo.", ct);
            return;
        }
        session.ReserveSend(now);
        // Reserva durable antes del SMS. Fallos/timeout conservan la cuota: nunca superar dos envíos inciertos.
        await db.SaveChangesAsync(ct);
        var result = await auth.SendAsync(patient.DocumentNumber, session.ContactId, TurnLanguage.Detect(input.Text), ct);
        session.State = result == OtpOutcome.Approved ? BindingState.AwaitingCode : BindingState.Ended;
        await FinishAsync(session, input, result switch
        {
            OtpOutcome.Approved => $"Enviamos un código por SMS a {MessagePolicy.MaskPhone(phone)}. Escribe los 6 dígitos; tienes hasta 3 intentos y 10 minutos desde el inicio. REENVIAR solicita otro código sin reiniciar los límites.",
            OtpOutcome.RateLimited => AuthLimit,
            _ => Unavailable,
        }, ct);
    }

    private async Task VerifyAsync(BindingSession session, WhatsAppRawEvent input, string code, CancellationToken ct)
    {
        if (!session.CanVerify(clock.GetUtcNow()))
        {
            session.State = BindingState.Ended;
            await FinishAsync(session, input, "Código inválido o proceso terminado. Envía VINCULAR para comenzar de nuevo.", ct);
            return;
        }
        var patient = await CurrentPatientAsync(session, ct);
        if (patient is null) { session.State = BindingState.Ended; await FinishAsync(session, input, Guide, ct); return; }
        session.Attempts++;
        session.State = BindingState.Verifying;
        await db.SaveChangesAsync(ct);
        var result = Regex.IsMatch(code, @"\A[0-9]{6}\z", RegexOptions.CultureInvariant)
            ? await auth.VerifyAsync(patient.DocumentNumber, code, ct) : new OtpVerificationResult(OtpOutcome.Rejected);
        if (result.Outcome == OtpOutcome.Approved)
        {
            var current = await CurrentPatientAsync(session, ct);
            if (current is null || current.UserId is null || current.UserId != result.UserId || session.IsExpired(clock.GetUtcNow()))
            { session.State = BindingState.Ended; await FinishAsync(session, input, Unavailable, ct); return; }
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var owned = await db.BindingSessions.FromSqlInterpolated($"""
                SELECT * FROM whatsapp.binding_sessions
                WHERE id = {session.Id} AND lease_owner = {session.LeaseOwner} AND lease_until > {clock.GetUtcNow()}
                FOR UPDATE
                """).AsNoTracking().ToListAsync(ct);
            if (owned.Count == 0)
            {
                db.Entry(session).State = EntityState.Unchanged;
                input.Reply = Unavailable;
                Scrub(input);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return;
            }
            var linkId = Guid.NewGuid();
            var now = clock.GetUtcNow();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO whatsapp.patient_links (id, patient_id, user_id, wa_id, first_name, language, opt_out, created_at, updated_at)
                VALUES ({linkId}, {current.PatientId}, {current.UserId}, {input.WaId}, {current.FirstName}, 'es', false, {now}, {now})
                ON CONFLICT DO NOTHING
                """, ct);
            var linked = await db.PatientLinks.AsNoTracking().AnyAsync(x => x.WaId == input.WaId && x.PatientId == current.PatientId, ct);
            session.State = linked ? BindingState.Approved : BindingState.Ended;
            await FinishAsync(session, input, linked ? "Número vinculado. Ya puedes conversar con tu asistente en tu siguiente mensaje." : Unavailable, ct);
            await transaction.CommitAsync(ct);
            return;
        }
        session.State = result.Outcome == OtpOutcome.Rejected && session.Attempts < 3 ? BindingState.AwaitingCode : BindingState.Ended;
        await FinishAsync(session, input, result.Outcome switch
        {
            OtpOutcome.RateLimited => AuthLimit,
            OtpOutcome.Unavailable => Unavailable,
            _ when session.Attempts >= 3 => "Código inválido o expirado. Agotaste los 3 intentos; la vinculación terminó. Envía VINCULAR para comenzar de nuevo.",
            _ => $"Código inválido o expirado. Quedan {3 - session.Attempts} intentos. Escribe únicamente los 6 dígitos del SMS.",
        }, ct);
    }

    private const string AuthLimit = "El servicio de verificación limitó las solicitudes. Intenta más tarde; no se enviará ni verificará otro código ahora.";

    private async Task FinishAsync(BindingSession session, WhatsAppRawEvent input, string reply, CancellationToken ct)
    {
        input.Reply = reply;
        Scrub(input);
        session.LastEventId = input.Id;
        session.LastReply = reply;
        session.LeaseOwner = null;
        session.LeaseUntil = null;
        await db.SaveChangesAsync(ct);
    }

    private static void Scrub(WhatsAppRawEvent input)
    {
        input.Text = "[vinculación]";
        input.Payload = "{}";
    }
}
