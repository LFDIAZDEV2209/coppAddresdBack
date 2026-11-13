using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Features.Sos;
using CoppAddresd.Application.WhatsApp.Messaging;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text.Json;

// Host de QA: solo loopback y BD desechable; ningún transporte externo.
var builder = WebApplication.CreateBuilder(args);
var cs = "Host=127.0.0.1;Port=55439;Database=copp_review_sos_whatsapp;Username=postgres";
builder.Configuration["SosWhatsApp:Enabled"] = "true";
builder.Configuration["SosWhatsApp:TemplateName"] = "copp_sos_alert_v1";
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs).AddInterceptors(new LocationSnapshot()));
builder.Services.AddScoped<ISosAlertRepository,SosAlertRepository>();
builder.Services.AddSingleton<ISosRateLimiter,Limiter>();
builder.Services.AddSingleton<ISosDispatchQueue,Queue>();
builder.Services.AddScoped<ActivateSosAlertHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<Sender>();
builder.Services.AddSingleton<IWhatsAppMessageSender>(sp=>new CoppAddresd.WhatsApp.Messaging.MetaCloudWhatsAppSender(
 new HttpClient(new Wire(sp.GetRequiredService<Sender>())),new CoppAddresd.WhatsApp.WhatsAppOptions {PhoneNumberId="qa-number",AccessToken="qa"},TimeProvider.System,
 Microsoft.Extensions.Logging.Abstractions.NullLogger<CoppAddresd.WhatsApp.Messaging.MetaCloudWhatsAppSender>.Instance));
// Registro por reflexión permite probar RED antes de que exista la implementación.
var register = typeof(AppDbContext).Assembly.GetType("CoppAddresd.Infrastructure.Sos.SosWhatsAppRegistration");
register?.GetMethod("AddSosWhatsApp")?.Invoke(null,[builder.Services,builder.Configuration]);
builder.Services.AddSingleton<IHostedService,CoppAddresd.Infrastructure.Sos.SosWhatsAppWorker>();
var app=builder.Build();
using(var scope=app.Services.CreateScope()) {
 var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
 await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS auth; CREATE TABLE IF NOT EXISTS auth.\"Users\" (\"Id\" uuid PRIMARY KEY, \"SecurityStamp\" text NULL);");
 await db.Database.MigrateAsync();
}
var capturedAt=DateTime.UtcNow;
var patient=Guid.Parse("a2906d14-f593-4892-aae5-8797a8c99311");
app.MapGet("/",()=>Results.Content("""
<!doctype html><html lang="es"><meta charset="utf-8"><body><h1>Prueba SOS WhatsApp</h1><button id="activate">Activar SOS de prueba</button><button id="retry">Repetir activación</button><pre id="result"></pre><pre id="deliveries"></pre><script>
const activate=async()=>{result.textContent=JSON.stringify(await(await fetch('/activate',{method:'POST'})).json());};document.querySelector('#activate').onclick=activate;document.querySelector('#retry').onclick=activate;
setInterval(async()=>{deliveries.textContent=JSON.stringify(await(await fetch('/deliveries')).json());},250);
</script></body></html>
""","text/html"));
app.MapPost("/activate",async(ActivateSosAlertHandler handler,AppDbContext db,CancellationToken ct)=>{
 if(!await db.PatientProfiles.AnyAsync(p=>p.Id==patient,ct)) { db.PatientProfiles.Add(new PatientProfile{Id=patient,FirstName="María",LastName="Gómez",EmergencyContact="{\"phone\":\"+12025550101\",\"secondary\":{\"phone\":\"+12025550102\"}}"}); await db.SaveChangesAsync(ct); }
 var result=await handler.Handle(new ActivateSosAlertCommand(patient,"7f58b560-cb08-4930-9a38-cceebd332023",10.99,-74.8,12,capturedAt,new SosVitalsDto(84,97,"120/80",capturedAt.AddMinutes(-2),capturedAt.AddMinutes(-2),capturedAt.AddMinutes(-2)),null),ct);
 // En QA sustituye únicamente el enriquecimiento externo de dirección.
 var alert=await db.SosAlerts.FirstAsync(a=>a.PatientId==patient,ct);alert.LocationDescription="Calle 114, Barranquilla, Colombia";alert.LocationLookupCompletedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);
 return Results.Json(new{outcome=result.Outcome.ToString()});
});
app.MapGet("/deliveries",(Sender sender)=>sender.Messages.ToArray());
app.MapGet("/states",async(AppDbContext db,CancellationToken ct)=>await db.SosWhatsAppDeliveries.AsNoTracking().OrderBy(x=>x.ContactOrder).Select(x=>new{x.Id,x.State,x.DeliveryStatus,x.Attempts,x.ErrorCode,x.ParametersJson}).ToArrayAsync(ct));
app.MapPost("/reset/{mode}",async(string mode,AppDbContext db,Sender sender,CancellationToken ct)=>{
 await db.SosWhatsAppDeliveries.ExecuteDeleteAsync(ct); await db.NotificationDedupeKeys.ExecuteDeleteAsync(ct);await db.SosAlerts.ExecuteDeleteAsync(ct);
 sender.Messages.Clear();sender.Mode=mode;sender.Calls=0;return Results.Ok();
});
app.MapPost("/advance",async(AppDbContext db,CancellationToken ct)=>{
 await db.SosWhatsAppDeliveries.Where(x=>x.State=="Pending").ExecuteUpdateAsync(s=>s.SetProperty(x=>x.NextAttemptAt,DateTime.UtcNow.AddSeconds(-1)),ct);return Results.Ok();
});
app.MapPost("/status/{state}",async(string state,AppDbContext db,CoppAddresd.Infrastructure.Sos.SosWhatsAppProcessor processor,CancellationToken ct)=>{
 var d=await db.SosWhatsAppDeliveries.AsNoTracking().OrderBy(x=>x.ContactOrder).FirstAsync(ct);
 var options=new CoppAddresd.WhatsApp.WhatsAppOptions{Enabled=true,AppSecret="qa-signature",WebhookVerifyToken="qa",PhoneNumberId="qa-number",AccessToken="qa"};
 var value=new {metadata=new{phone_number_id="qa-number"},statuses=new[]{new{id=d.Wamid,recipient_id=d.Destination.TrimStart('+'),status=state=="invalid"?"read":state,timestamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),biz_opaque_callback_data="sos:"+d.Id}}};
 var bytes=System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{ @object="whatsapp_business_account",entry=new[]{new{changes=new[]{new{value}}}}}));
 var signature="sha256="+Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(options.AppSecret),bytes)).ToLowerInvariant();
 await using var wa=new CoppAddresd.WhatsApp.Persistence.WhatsAppDbContext(new DbContextOptionsBuilder<CoppAddresd.WhatsApp.Persistence.WhatsAppDbContext>().UseNpgsql(cs).Options);
 var webhook=new CoppAddresd.WhatsApp.Webhook.WhatsAppWebhook(wa,options,new(),TimeProvider.System,Microsoft.Extensions.Logging.Abstractions.NullLogger<CoppAddresd.WhatsApp.Webhook.WhatsAppWebhook>.Instance,processor);
 return Results.Json(new{code=await webhook.AcceptAsync(bytes,state=="invalid"?"invalid":signature,ct)});
});
app.Run("http://127.0.0.1:5199");
sealed class Sender:IWhatsAppMessageSender {
 public ConcurrentQueue<object> Messages {get;}=new();
 public string Mode="normal"; public int Calls;
 public Task ShowTypingAsync(string id,CancellationToken ct)=>Task.CompletedTask;
 public Task<WhatsAppSendResult> SendTextAsync(string id,string text,DateTimeOffset at,CancellationToken ct)=>throw new InvalidOperationException("No texto libre");
 public Task<WhatsAppSendResult> SendTemplateAsync(string id,string name,string language,IReadOnlyList<string> parameters,CancellationToken ct) { var call=Interlocked.Increment(ref Calls);
 if(Mode=="timeout") return Task.FromResult(WhatsAppSendResult.Failed("timeout",WhatsAppFailureKind.Transient));
 if(Mode=="rate" && call==1)return Task.FromResult(WhatsAppSendResult.Failed("http_429",WhatsAppFailureKind.Transient));
 Messages.Enqueue(new{destination=id,name,language,parameters});return Task.FromResult(new WhatsAppSendResult("wamid.qa."+id)); }
}
sealed class Limiter:ISosRateLimiter {
 public Task<SosRateLimitDecision> CheckAsync(Guid patientId,string destinationPhone,string? deviceId,CancellationToken ct=default)=>Task.FromResult(SosRateLimitDecision.Allow());
 public Task RegisterAttemptAsync(Guid patientId,string destinationPhone,string? deviceId,CancellationToken ct=default)=>Task.CompletedTask;
}
sealed class Queue:ISosDispatchQueue {
 public ValueTask EnqueueAsync(Guid id,CancellationToken ct=default)=>ValueTask.CompletedTask;
 public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct=default)=>throw new NotSupportedException();
}

// Ejercita JSON y correlación del adaptador productivo; no tiene conexión de red.
sealed class Wire(Sender sender):HttpMessageHandler {
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
  if(request.RequestUri?.Host!="graph.facebook.com")throw new InvalidOperationException("Destino de red inesperado");
  using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));var p=json.RootElement;
  if(!p.GetProperty("biz_opaque_callback_data").GetString()!.StartsWith("sos:"))throw new InvalidOperationException("Falta correlación");
  var t=p.GetProperty("template");var parameters=t.GetProperty("components")[0].GetProperty("parameters").EnumerateArray().Select(x=>x.GetProperty("text").GetString()!).ToArray();
  var result=await sender.SendTemplateAsync("+"+p.GetProperty("to").GetString(),t.GetProperty("name").GetString()!,t.GetProperty("language").GetProperty("code").GetString()!,parameters,ct);
  if(result.ErrorCode=="timeout")throw new HttpRequestException("Synthetic ambiguous network outcome");
  return new HttpResponseMessage(result.Succeeded?System.Net.HttpStatusCode.OK:System.Net.HttpStatusCode.TooManyRequests) {
   Content=new StringContent(result.Succeeded?JsonSerializer.Serialize(new{messages=new[]{new{id=result.Wamid}}}):"{\"error\":{\"code\":130429}}",System.Text.Encoding.UTF8,"application/json")};
 }
}

sealed class LocationSnapshot:Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor {
 public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData data,Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,CancellationToken ct=default) {
  foreach(var e in data.Context!.ChangeTracker.Entries<SosAlert>().Where(x=>x.State==EntityState.Added)) {e.Entity.LocationDescription="Calle 114, Barranquilla, Colombia";e.Entity.LocationLookupCompletedAt=DateTime.UtcNow;}
  return ValueTask.FromResult(result);
 }
}
