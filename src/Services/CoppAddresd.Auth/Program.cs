using System.Threading.RateLimiting;
using CoppAddresd.Auth.Authorization;
using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Extensions;
using CoppAddresd.Auth.Middleware;
using CoppAddresd.Auth.Security;
using CoppAddresd.Auth.Seeders;
using CoppAddresd.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

ValidateConfiguration(builder.Configuration);

builder.Services.AddControllers();
// IP del cliente para el motor de protección OTP (misma fuente que el rate
// limiter global: HttpContext.Connection.RemoteIpAddress).
builder.Services.AddHttpContextAccessor();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CoppAddresd Auth API",
        Version = "v1",
        Description = "Microservicio de autenticación y autorización"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese el token JWT"
    });
});

builder.Services.AddAuthDatabase(builder.Configuration);
builder.Services.AddAuthIdentity();
builder.Services.AddAuthJwt(builder.Configuration);
builder.Services.AddAuthCors(builder.Configuration);

builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection(AuthSettings.SectionName));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.Configure<TwilioSettings>(builder.Configuration.GetSection(TwilioSettings.SectionName));
builder.Services.Configure<OtpSecuritySettings>(builder.Configuration.GetSection(OtpSecuritySettings.SectionName));

// Cliente Twilio (Singleton, stateless-safe). Autenticación por API Key
// (ApiKeySid + ApiKeySecret, Basic Auth sobre el SDK) — nunca el Auth Token
// maestro. Las llamadas reales solo ocurren si IsEnabled y siempre vía
// TwilioOtpService (que valida configuración antes de contactar al proveedor).
builder.Services.AddSingleton<Twilio.Clients.ITwilioRestClient>(serviceProvider =>
{
    var twilio = serviceProvider.GetRequiredService<IOptions<TwilioSettings>>().Value;
    return new Twilio.Clients.TwilioRestClient(
        twilio.ApiKeySid,
        twilio.ApiKeySecret,
        twilio.AccountSid,
        region: null,
        httpClient: new Twilio.Http.SystemNetHttpClient(new HttpClient()),
        edge: null);
});

builder.Services.AddScoped<ITwilioOtpService, TwilioOtpService>();

// Motor de protección OTP en memoria: Singleton porque mantiene contadores y
// estado compartidos entre solicitudes dentro de la misma instancia (Scoped
// daría a cada request su propio estado, inútil para rate limiting).
builder.Services.AddSingleton<IOtpProtectionService, OtpProtectionService>();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IPatientLookupService, PatientLookupService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IScopedPermissionService, ScopedPermissionService>();
builder.Services.AddScoped<IInvitationService, InvitationService>();
builder.Services.AddScoped<ITokenInvalidationService, TokenInvalidationService>();
// Cualificado: existe Microsoft.AspNetCore.Identity.SecurityStampValidator con el mismo nombre.
builder.Services.AddScoped<CoppAddresd.Auth.Security.ISecurityStampValidator, CoppAddresd.Auth.Security.SecurityStampValidator>();

// Correos transaccionales: "Log" en dev (imprime en el logger), "Smtp" en prod.
var emailProvider = builder.Configuration["Email:Provider"] ?? "Log";
if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, LogEmailSender>();
}

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ErpAudienceHandler>();

// REQ-AUDIT-03: los endpoints de administración ERP (Users/Roles/Permissions)
// exigen aud == "erp". Un token "app" (sin stamp check) no puede invocarlos.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(ErpAudienceRequirement.PolicyName, policy =>
        policy.AddRequirements(new ErpAudienceRequirement()));
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 10
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"Demasiadas peticiones. Intenta más tarde.\"}",
            cancellationToken);
    };
});

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!, name: "postgresql");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<AuthDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    await dbContext.Database.MigrateAsync();

    await PermissionSeeder.SeedAsync(dbContext, logger);

    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
    var authSettings = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthSettings>>().Value;

    await AdminSeeder.SeedAsync(dbContext, userManager, roleManager, authSettings, logger);
    await ApplicationSeeder.SeedAsync(dbContext, userManager, authSettings.AdminEmail, logger);
    await RoleSeeder.SeedAsync(dbContext, logger);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CoppAddresd Auth API v1");
    });
}

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseCors();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

static void ValidateConfiguration(IConfiguration configuration)
{
    var jwtSecret = configuration["Jwt:Secret"];
    if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
    {
        throw new InvalidOperationException("Jwt:Secret must be at least 32 characters long");
    }

    var connectionString = configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required");
    }

    // Twilio solo es obligatorio cuando está habilitado: Development puede
    // arrancar sin credenciales (IsEnabled = false en appsettings). Cuando se
    // habilita, las cuatro propiedades son obligatorias para no llegar a
    // runtime con configuración incompleta.
    var twilio = new TwilioSettings();
    configuration.GetSection(TwilioSettings.SectionName).Bind(twilio);

    if (twilio.IsEnabled)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(twilio.AccountSid)) missing.Add("AccountSid");
        if (string.IsNullOrWhiteSpace(twilio.ApiKeySid)) missing.Add("ApiKeySid");
        if (string.IsNullOrWhiteSpace(twilio.ApiKeySecret)) missing.Add("ApiKeySecret");
        if (string.IsNullOrWhiteSpace(twilio.VerifyServiceSid)) missing.Add("VerifyServiceSid");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Twilio está habilitado (Twilio:IsEnabled) pero faltan las credenciales: " +
                $"{string.Join(", ", missing)}. Configúralas (appsettings o variables de " +
                "entorno TWILIO__*) o deshabilita Twilio.");
        }
    }

    // Validación básica de los límites de seguridad del flujo OTP. Solo se
    // comprueban invariantes estructurales (mayor que cero / no negativo); el
    // mecanismo de protección aún no está implementado.
    var otpSecurity = new OtpSecuritySettings();
    configuration.GetSection(OtpSecuritySettings.SectionName).Bind(otpSecurity);

    RequirePositive(otpSecurity.SendPerIpPerMinute, nameof(OtpSecuritySettings.SendPerIpPerMinute));
    RequirePositive(otpSecurity.SendPerIpPerHour, nameof(OtpSecuritySettings.SendPerIpPerHour));
    RequirePositive(otpSecurity.SendPerPhonePerMinute, nameof(OtpSecuritySettings.SendPerPhonePerMinute));
    RequirePositive(otpSecurity.SendPerPhonePerHour, nameof(OtpSecuritySettings.SendPerPhonePerHour));
    RequirePositive(otpSecurity.SendPerPhonePerDay, nameof(OtpSecuritySettings.SendPerPhonePerDay));
    RequireNonNegative(otpSecurity.SendPhoneCooldownSeconds, nameof(OtpSecuritySettings.SendPhoneCooldownSeconds));
    RequirePositive(otpSecurity.SendPerDocumentPerHour, nameof(OtpSecuritySettings.SendPerDocumentPerHour));
    RequirePositive(otpSecurity.VerifyPerIpPerMinute, nameof(OtpSecuritySettings.VerifyPerIpPerMinute));
    RequirePositive(otpSecurity.VerifyPerPhoneWindowMinutes, nameof(OtpSecuritySettings.VerifyPerPhoneWindowMinutes));
    RequirePositive(otpSecurity.VerifyPerPhoneLimit, nameof(OtpSecuritySettings.VerifyPerPhoneLimit));
    RequirePositive(otpSecurity.VerifyPhoneMaxFailedAttempts, nameof(OtpSecuritySettings.VerifyPhoneMaxFailedAttempts));
    RequireNonNegative(otpSecurity.VerifyPhoneLockoutSeconds, nameof(OtpSecuritySettings.VerifyPhoneLockoutSeconds));

    static void RequirePositive(int value, string propertyName)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException(
                $"OtpSecurity:{propertyName} debe ser mayor que 0 (valor actual: {value}).");
        }
    }

    static void RequireNonNegative(int value, string propertyName)
    {
        if (value < 0)
        {
            throw new InvalidOperationException(
                $"OtpSecurity:{propertyName} no puede ser negativo (valor actual: {value}).");
        }
    }
}
