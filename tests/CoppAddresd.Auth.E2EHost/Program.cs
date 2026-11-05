using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using CoppAddresd.Auth.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Twilio.Clients;
using Twilio.Http;

// Auth e Identity reales; solo se sustituye la red de Twilio. Nunca usar con datos reales.
var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? "");
if (connection.Database?.StartsWith("copp_review_", StringComparison.Ordinal) != true || connection.Host != "127.0.0.1")
    throw new InvalidOperationException("Se requiere PostgreSQL local y una base copp_review_ aislada.");
var output = Path.GetFullPath(Environment.GetEnvironmentVariable("COP_E2E_OUTPUT") ?? "");
if (!output.StartsWith(Path.GetTempPath(), StringComparison.Ordinal))
    throw new InvalidOperationException("La evidencia debe quedar en un directorio temporal.");
await using var factory = new AuthFactory();
factory.UseKestrel(options => options.ListenLocalhost(5125));
using var client = factory.CreateClient();
Console.WriteLine("Auth E2E listo en 5125: Identity/PostgreSQL reales y Twilio aislado.");
await Task.Delay(Timeout.Infinite);

sealed class AuthFactory : WebApplicationFactory<AuthDbContext>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Review");
        builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Services/CoppAddresd.Auth")));
        builder.UseSetting("SkipDatabaseInitialization", "true");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITwilioRestClient>();
            services.AddSingleton<ITwilioRestClient, IsolatedVerifyClient>();
        });
    }
}

sealed class IsolatedVerifyClient : ITwilioRestClient
{
    private readonly ConcurrentDictionary<string, bool> pending = new();
    public string AccountSid => "AC_e2e_only";
    public string Region => null!;
    public Twilio.Http.HttpClient HttpClient => null!;
    public Response Request(Request request) => throw new NotSupportedException();
    public Task<Response> RequestAsync(Request request)
    {
        var phone = request.PostParams.FirstOrDefault(p => p.Key == "To").Value ?? "";
        var code = request.PostParams.FirstOrDefault(p => p.Key == "Code").Value;
        if (!request.Uri.AbsolutePath.EndsWith("/VerificationCheck", StringComparison.Ordinal))
        {
            pending[phone] = true;
            return Reply(HttpStatusCode.Created, new { sid = "VE_e2e_only", status = "pending", to = phone });
        }
        if (code == "000000" || !pending.ContainsKey(phone))
            throw new Twilio.Exceptions.ApiException(20404, 404, "Synthetic expired or consumed verification", "", null, null);
        var approved = code == "123456";
        if (approved) pending.TryRemove(phone, out _);
        return Reply(HttpStatusCode.OK, new { sid = "VE_e2e_only", status = approved ? "approved" : "pending", valid = approved, to = phone });
    }
    private static Task<Response> Reply(HttpStatusCode status, object body) => Task.FromResult(new Response(status, JsonSerializer.Serialize(body)));
}
