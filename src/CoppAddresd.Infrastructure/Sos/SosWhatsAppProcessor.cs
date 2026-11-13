using System.Text.Json;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.WhatsApp.Messaging;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Sos;

/// <summary>Claim atómico entre réplicas; HTTP fuera de transacción. Incertidumbre no se reenvía.</summary>
public sealed class SosWhatsAppProcessor(AppDbContext db, ISosAlertRepository alerts,
    IWhatsAppMessageSender sender, IOptions<SosWhatsAppSettings> settings, TimeProvider clock,
    ISosLocationLinkService? links = null, SosLocationEnricher? locations = null) : ISosWhatsAppStatusReceiver
{
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        if (!settings.Value.Enabled) return false;
        var now=clock.GetUtcNow().UtcDateTime;
        // Una caída después de enviar puede haber alcanzado Meta. No repetir a ciegas.
        await db.SosWhatsAppDeliveries.Where(x=>x.State=="Processing" && x.LeaseUntil<now)
            .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Unknown").SetProperty(x=>x.ErrorCode,"lease_expired"),ct);
        var id=await db.SosWhatsAppDeliveries.AsNoTracking().Where(x=>x.State=="Pending" && x.NextAttemptAt<=now)
            .OrderBy(x=>x.NextAttemptAt).ThenBy(x=>x.Id).Select(x=>(Guid?)x.Id).FirstOrDefaultAsync(ct);
        if(id is null) return false;
        var token=Guid.NewGuid();
        var claimed=await db.SosWhatsAppDeliveries.Where(x=>x.Id==id && x.State=="Pending" && x.NextAttemptAt<=now)
            .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,"Processing").SetProperty(x=>x.LeaseToken,token)
                .SetProperty(x=>x.LeaseUntil,now.AddMinutes(2)).SetProperty(x=>x.Attempts,x=>x.Attempts+1),ct);
        if(claimed==0) return true;
        var delivery=await db.SosWhatsAppDeliveries.AsNoTracking().SingleAsync(x=>x.Id==id,ct);
        var alert=await alerts.GetByIdAsync(delivery.AlertId,ct);
        if(alert is null || alert.Status!=SosAlertStatus.Activa || alert.CreatedAt<now.AddMinutes(-15)) {
            await Finish("Cancelled","alert_inactive_or_expired",null);return true;
        }
        string[] parameters;
        try {
            if(delivery.ParametersJson is null) {
                if(locations is not null) await locations.EnrichAsync(alert,ct);
                parameters=SosWhatsAppTemplate.Parameters(alert,delivery.PatientName,links?.CreateLink(alert));
                var json=JsonSerializer.Serialize(parameters);
                await db.SosWhatsAppDeliveries.Where(x=>x.Id==id && x.LeaseToken==token)
                    .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ParametersJson,json),ct);
            } else parameters=JsonSerializer.Deserialize<string[]>(delivery.ParametersJson)!;
            // Releer cancelación antes de iniciar HTTP.
            var current=await alerts.GetByIdAsync(delivery.AlertId,ct);
            if(current?.Status!=SosAlertStatus.Activa) {await Finish("Cancelled","alert_inactive",null);return true;}
            var result=await sender.SendTemplateWithCallbackAsync(delivery.Destination,settings.Value.TemplateName,
                settings.Value.Language,parameters,"sos:"+id,ct);
            if(result.Succeeded) await Finish("Accepted",null,result.Wamid);
            else {
                var ambiguous=result.ErrorCode is "timeout" or "network" || (result.ErrorCode?.StartsWith("http_2",StringComparison.Ordinal)==true);
                // Reintento solo tras rechazo explícito (429/5xx); nunca después de un ACK incierto.
                var retry=result.Failure==WhatsAppFailureKind.Transient && !ambiguous && delivery.Attempts<3;
                await Finish(ambiguous?"Unknown":retry?"Pending":"Failed",result.ErrorCode,null);
            }
        }
        catch(OperationCanceledException) {throw;} // Lease vencido queda Unknown; nunca se duplica al reiniciar.
        catch(Exception) {await Finish("Unknown","unexpected_failure",null);}
        return true;

        async Task Finish(string state,string? error,string? wamid) {
            await db.SosWhatsAppDeliveries.Where(x=>x.Id==id && x.LeaseToken==token)
                .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,state).SetProperty(x=>x.ErrorCode,x=>x.DeliveryRank>0?x.ErrorCode:error)
                    .SetProperty(x=>x.Wamid,x=>x.Wamid??wamid).SetProperty(x=>x.UpdatedAt,now)
                    .SetProperty(x=>x.NextAttemptAt,now.AddSeconds(15*Math.Pow(2,delivery.Attempts-1)))
                    .SetProperty(x=>x.LeaseUntil,(DateTime?)null),ct);
        }
    }

    public async Task ReceiveAsync(string callbackData,string wamid,string recipient,string status,DateTime eventAt,string? errorCode,CancellationToken ct)
    {
        if(!callbackData.StartsWith("sos:",StringComparison.Ordinal) || !Guid.TryParse(callbackData[4..],out var id))return;
        var rank=status switch {"sent"=>1,"failed"=>2,"delivered"=>3,"read"=>4,_=>0};
        if(rank==0 || wamid.Length>512 || recipient.Length is <8 or >15 || !recipient.All(char.IsAsciiDigit))return;
        // El ID opaco referencia la reserva previa a HTTP; sirve aun si Meta llama antes del ACK.
        await db.SosWhatsAppDeliveries.Where(x=>x.Id==id && x.Destination=="+"+recipient
            && (x.Wamid==null || x.Wamid==wamid) && x.DeliveryRank<rank && x.Attempts>0)
            .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Wamid,wamid).SetProperty(x=>x.DeliveryStatus,status)
                .SetProperty(x=>x.DeliveryRank,rank).SetProperty(x=>x.UpdatedAt,eventAt)
                .SetProperty(x=>x.ErrorCode,errorCode),ct);
    }
}
