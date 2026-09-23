using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using CoppAddresd.Auth.Avatar.Application;
using CoppAddresd.Auth.Controllers;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Infrastructure.Avatar;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace CoppAddresd.UnitTests.Auth;

/// <summary>HTTP + JWT + PostgreSQL real; todos los datos de prueba se revierten.</summary>
public class AvatarPostgresTests
{
    [Fact]
    public async Task DemoAccount_ConfigurationSurvivesNewConnection_WhenExplicitlyEnabled()
    {
        var document=Environment.GetEnvironmentVariable("COP_AVATAR_DEMO_DOCUMENT");
        var connectionString=Environment.GetEnvironmentVariable("COP_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(document) || string.IsNullOrWhiteSpace(connectionString)) return;
        var options=new DbContextOptionsBuilder<AuthDbContext>().UseNpgsql(connectionString).Options;
        Guid id;
        var selection=AvatarConfiguration.Default("female") with { Accessories=new("glasses-01","watch-01","bracelet-01") };
        await using(var first=new AuthDbContext(options)) {
            id=await first.Database.SqlQuery<Guid>($"""SELECT user_id AS "Value" FROM app.patient_profiles WHERE document_number={document}""").SingleAsync();
            var store=new AvatarPreferenceStore(first);
            var prior=await store.ReadAsync(id,CancellationToken.None);
            var recovery=Environment.GetEnvironmentVariable("COP_AVATAR_RECOVERY_FILE");
            if (!string.IsNullOrEmpty(recovery) && !File.Exists(recovery)) await File.WriteAllTextAsync(recovery,prior??"null");
            Assert.True(await new AvatarConfigurationUseCases(store).PutAsync(id,selection,CancellationToken.None));
        }
        await using var second=new AuthDbContext(options);
        Assert.Equal(selection,await new AvatarConfigurationUseCases(new AvatarPreferenceStore(second)).GetAsync(id,CancellationToken.None));
    }

    [Fact]
    public async Task JwtOwnership_Upsert_ReadBack_AndPreferencesPreserved()
    {
        var connectionString = Environment.GetEnvironmentVariable("COP_TEST_DB_CONNECTION");
        if (string.IsNullOrEmpty(connectionString)) return; // Ejecución explícita con BD local en la misión.
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        var a=Guid.NewGuid(); var b=Guid.NewGuid();
        // No se crean mediciones ni perfiles clínicos; las identidades desaparecen con rollback.
        await using (var seed = new NpgsqlCommand("""
            INSERT INTO auth."Users" ("Id","FirstName","LastName","IsActive","CreatedAt","EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount")
            VALUES (@a,'Avatar','Test',true,now(),false,false,false,false,0),(@b,'Avatar','Test',true,now(),false,false,false,false,0);
            INSERT INTO auth."UserPreferences" ("UserId","Lang","AccentColor","UpdatedAt") VALUES (@a,'en','#123456',now());
            """,connection,tx)) { seed.Parameters.AddWithValue("a",a);seed.Parameters.AddWithValue("b",b);await seed.ExecuteNonQueryAsync(); }
        var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder => {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services => {
                    services.AddControllers().AddApplicationPart(typeof(AvatarConfigurationController).Assembly);
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => {
                        options.TokenValidationParameters=new() { ValidateIssuer=false,ValidateAudience=false,IssuerSigningKey=key,ValidateLifetime=true };
                    });
                    services.AddAuthorization();
                    services.AddScoped(_ => {var db=new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>().UseNpgsql(connection).Options);db.Database.UseTransaction(tx);return db;});
                    services.AddScoped<IAvatarPreferenceStore,AvatarPreferenceStore>();services.AddScoped<AvatarConfigurationUseCases>();
                });
                webBuilder.Configure(app => {app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.UseEndpoints(e=>e.MapControllers());});
            })
            .StartAsync();
        using var client = host.GetTestServer().CreateClient();
        const string url="/api/auth/me/avatar";
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(url)).StatusCode);
        string Token(Guid id) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(claims:[new Claim(ClaimTypes.NameIdentifier,id.ToString())],expires:DateTime.UtcNow.AddMinutes(5),signingCredentials:new(key,SecurityAlgorithms.HmacSha256)));
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",Token(a));
        var defaults=await client.GetFromJsonAsync<AvatarConfiguration>(url);Assert.NotNull(defaults);Assert.Equal("male",defaults.Gender);
        var selected=AvatarConfiguration.Default("female") with { Accessories=new("glasses-01","watch-01","bracelet-01") };
        // El parámetro se llama configuration, pero el body es el DTO directamente.
        // Un wrapper no corrige un fallo de deserialización y no debe escribir preferencias.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PutAsJsonAsync(url, new { configuration = selected })).StatusCode);
        Assert.Equal(defaults, await client.GetFromJsonAsync<AvatarConfiguration>(url));
        Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync(url+"?userId="+b,selected)).StatusCode);
        Assert.Equal(selected,await client.GetFromJsonAsync<AvatarConfiguration>(url));
        foreach (var hair in new[] { "female-hair-long-01", "female-hair-long-02", "female-hair-long-03" }) {
            var longHair = selected with { Hair = hair };
            Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync(url,longHair)).StatusCode);
            Assert.Equal(longHair,await client.GetFromJsonAsync<AvatarConfiguration>(url));
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync(url,longHair with { Gender="male" })).StatusCode);
        }
        foreach (var gender in new[] { "male", "female" }) {
            foreach (var tone in new[] { "skin-01", "skin-02", "skin-03", "skin-04", "skin-05" }) {
                foreach (var style in new[] { "01", "02", "03" }) {
                    var full = AvatarConfiguration.Default(gender) with { Skin=tone,
                        Hair=gender=="female"?"female-hair-long-01":"hair-02",
                        Clothing=new("shirt-basic-01",$"pants-{gender}-{style}",$"shoes-{gender}-01"),
                        Accessories=new("glasses-01","watch-01","bracelet-01") };
                    Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync(url,full)).StatusCode);
                    Assert.Equal(full,await client.GetFromJsonAsync<AvatarConfiguration>(url));
                    Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync(url,full with { Gender=gender=="male"?"female":"male" })).StatusCode);
                }
            }
        }
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync(url,selected with { Skin="invalid" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync(url,selected with {Hair="hair-03"})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync(url,new {userId=b,weight=90})).StatusCode);
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",Token(b));
        Assert.Equal("male",(await client.GetFromJsonAsync<AvatarConfiguration>(url+"?userId="+a))!.Gender);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(url+"/"+a)).StatusCode);
        await using(var check=new NpgsqlCommand("""SELECT "Lang" || ':' || "AccentColor" FROM auth."UserPreferences" WHERE "UserId"=@a""",connection,tx)) {
            check.Parameters.AddWithValue("a",a);Assert.Equal("en:#123456",await check.ExecuteScalarAsync());
        }
        await tx.RollbackAsync();
    }
}
