using CoppAddresd.Gateway.Configuration;
using CoppAddresd.Gateway.HealthChecks;
using CoppAddresd.Gateway.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Configuración tipada del gateway.
builder.Services.Configure<InternalKeySettings>(
    builder.Configuration.GetSection("InternalKey"));
builder.Services.Configure<CorsSettings>(
    builder.Configuration.GetSection("Cors"));

// CORS centralizado (REQ-GW-005): credenciales + header X-Refresh-Status expuesto.
builder.Services.AddCors(options =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
        ?? ["http://localhost:3000"];
    options.AddPolicy("GatewayCors", policy => policy
        .WithOrigins(origins)
        .AllowCredentials()
        .AllowAnyMethod()
        .AllowAnyHeader()
        .WithExposedHeaders("X-Refresh-Status"));
});

// Reverse proxy declarativo (REQ-GW-002): rutas + clusters desde appsettings.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Sondas activas por cluster para el /health del gateway (REQ-GW-009).
// Cada cluster hace GET {BaseUrl}/{HealthCheckPath} con timeout de 2 s.
var clusterConfigs = builder.Configuration.GetSection("Clusters").GetChildren()
    .ToDictionary(c => c.Key, c => new
    {
        BaseUrl = c["BaseUrl"] ?? "",
        HealthCheckPath = c["HealthCheckPath"] ?? "/health"
    });

builder.Services.AddHealthChecks();
foreach (var (clusterId, cfg) in clusterConfigs)
{
    if (string.IsNullOrEmpty(cfg.BaseUrl)) continue;
    builder.Services.AddHttpClient($"health-{clusterId}")
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(2)
        })
        .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(2));
    // HealthCheckRegistration acepta una fábrica Func<IServiceProvider, IHealthCheck>;
    // ASP.NET Core resuelve la sonda y llama CheckHealthAsync de forma asíncrona.
    builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
        clusterId,
        sp => new ClusterHealthCheck(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient($"health-{clusterId}"),
            clusterId, cfg.BaseUrl, cfg.HealthCheckPath),
        failureStatus: HealthStatus.Unhealthy,
        tags: null));
}

var app = builder.Build();

// Pipeline: internals primero, luego CORS, luego proxy/health.
app.UseMiddleware<InternalKeyMiddleware>();
app.UseCors("GatewayCors");

app.MapReverseProxy();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync
});

app.Run();

/// <summary>Exposición para los tests de integración (WebApplicationFactory).</summary>
public partial class Program;
