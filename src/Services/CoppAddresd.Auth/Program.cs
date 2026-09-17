using System.Threading.RateLimiting;
using CoppAddresd.Auth.Authorization;
using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Extensions;
using CoppAddresd.Auth.Interfaces;
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
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Structured logging (Serilog). Sinks come from the "Serilog" configuration
// section (appsettings.Development.json enables Console + rolling compact-JSON
// file sink "logs/auth-.log"). Enrich.FromLogContext exposes LogContext
// properties (CorrelationId) on every event. When no Serilog:WriteTo section
// is configured (non-Development environments) it falls back to console-only,
// preserving the previous default console logging behavior: Information for
// app code, Microsoft.AspNetCore lowered to Warning (same verbosity as
// appsettings.Example.json Logging:LogLevel), and a template that includes
// {Properties:j} so CorrelationId/RequestPath stay visible on console.
builder.Host.UseSerilog((ctx, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext();

    if (!ctx.Configuration.GetSection("Serilog:WriteTo").GetChildren().Any())
    {
        cfg.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
    }
});

ValidateConfiguration(builder.Configuration);

builder.Services.AddControllers();

// IP del cliente para el motor de protecciÃ³n OTP (misma fuente que el rate
// limiter global: HttpContext.Connection.RemoteIpAddress).
builder.Services.AddHttpContextAccessor();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "CoppAddresd Auth API",
            Version = "v1",
            Description = "Microservicio de autenticaciÃ³n y autorizaciÃ³n",
        }
    );

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Ingrese el token JWT",
        }
    );
});

builder.Services.AddAuthDatabase(builder.Configuration);
builder.Services.AddAuthIdentity();
builder.Services.AddAuthJwt(builder.Configuration);
builder.Services.AddAuthCors(builder.Configuration);

// CachÃ© distribuida (Valkey) para catÃ¡logos de autorizaciÃ³n: provider por
// Cache:Provider (Valkey|Memory|None), fail-open por operaciÃ³n. Ver
// docs/modules/cache/README.md.
builder.Services.AddAuthCache(builder.Configuration);

builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection(AuthSettings.SectionName));
builder.Services.Configure<DevPatientSettings>(builder.Configuration.GetSection(DevPatientSettings.SectionName));
builder.Services.Configure<CommunityDemoSettings>(builder.Configuration.GetSection(CommunityDemoSettings.SectionName));
builder.Services.Configure<PatientAccountDemoSettings>(builder.Configuration.GetSection(PatientAccountDemoSettings.SectionName));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.Configure<TwilioSettings>(builder.Configuration.GetSection(TwilioSettings.SectionName));
builder.Services.Configure<OtpSecuritySettings>(builder.Configuration.GetSection(OtpSecuritySettings.SectionName));

// Cliente Twilio (Singleton, stateless-safe). AutenticaciÃ³n por API Key
// (ApiKeySid + ApiKeySecret, Basic Auth sobre el SDK) â€” nunca el Auth Token
// maestro. Las llamadas reales solo ocurren si IsEnabled y siempre vÃ­a
// TwilioOtpService (que valida configuraciÃ³n antes de contactar al proveedor).
builder.Services.AddSingleton<Twilio.Clients.ITwilioRestClient>(serviceProvider =>
{
    var twilio = serviceProvider.GetRequiredService<IOptions<TwilioSettings>>().Value;
    return new Twilio.Clients.TwilioRestClient(
        twilio.ApiKeySid,
        twilio.ApiKeySecret,
        twilio.AccountSid,
        region: null,
        httpClient: new Twilio.Http.SystemNetHttpClient(new HttpClient()),
        edge: null
    );
});

builder.Services.AddScoped<ITwilioOtpService, TwilioOtpService>();

// Motor de protecciÃ³n OTP en memoria: Singleton porque mantiene contadores y
// estado compartidos entre solicitudes dentro de la misma instancia (Scoped
// darÃ­a a cada request su propio estado, inÃºtil para rate limiting).
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
builder.Services.AddScoped<IDemoPatientSeedService, DemoPatientSeedService>();
builder.Services.AddScoped<IUserPreferenceService, UserPreferenceService>();
builder.Services.AddScoped<CoppAddresd.Auth.Avatar.Application.IAvatarPreferenceStore,
    CoppAddresd.Auth.Infrastructure.Avatar.AvatarPreferenceStore>();
builder.Services.AddScoped<CoppAddresd.Auth.Avatar.Application.AvatarConfigurationUseCases>();
builder.Services.AddScoped<ITokenInvalidationService, TokenInvalidationService>();

// Cualificado: existe Microsoft.AspNetCore.Identity.SecurityStampValidator con el mismo nombre.
builder.Services.AddScoped<
    CoppAddresd.Auth.Security.ISecurityStampValidator,
    CoppAddresd.Auth.Security.SecurityStampValidator
>();

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

// REQ-AUDIT-03: los endpoints de administraciÃ³n ERP (Users/Roles/Permissions)
// exigen aud == "erp". Un token "app" (sin stamp check) no puede invocarlos.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        ErpAudienceRequirement.PolicyName,
        policy => policy.AddRequirements(new ErpAudienceRequirement())
    );
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(
        "auth",
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 10,
                }
            )
    );

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"Demasiadas peticiones. Intenta mÃ¡s tarde.\"}",
            cancellationToken
        );
    };
});

builder
    .Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!, name: "postgresql");

var app = builder.Build();

// Actualización de binarios sobre una BD ya preparada, sin migraciones ni seeders.
if (!builder.Configuration.GetValue<bool>("SkipDatabaseInitialization"))
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<AuthDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    // Recoloca el historial desde public."__EFMigrationsHistory" (compartido
    // con el backend) hacia auth.__ef_migrations_history. Sin esto, en bases
    // ya migradas EF reintenta InitialCreate y falla con 42P07.
    await AuthMigrationHistoryRelocator.RelocateAsync(dbContext, logger);

    await dbContext.Database.MigrateAsync();

    await PermissionSeeder.SeedAsync(dbContext, logger);

    // Permisos del mÃ³dulo Program Progress (Program.*). Debe correr ANTES de
    // AdminSeeder para que el rol Admin reciba los 5 cÃ³digos por convenciÃ³n
    // (AdminSeeder asigna todos los permisos existentes al rol Admin).
    await ProgramProgressPermissionsSeeder.SeedAsync(dbContext, logger);

    // Permisos del mÃ³dulo Tests de Salud (HealthTests.*). Idem: antes de
    // AdminSeeder para que el rol Admin reciba los cÃ³digos por convenciÃ³n.
    await HealthTestsPermissionsSeeder.SeedAsync(dbContext, logger);

    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
    var authSettings = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthSettings>>().Value;
    var devPatientSettings = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DevPatientSettings>>().Value;
    var communityDemoSettings = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<CommunityDemoSettings>>().Value;
    var patientAccountDemoSettings = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<PatientAccountDemoSettings>>().Value;

    await AdminSeeder.SeedAsync(dbContext, userManager, roleManager, authSettings, logger);
    await ApplicationSeeder.SeedAsync(dbContext, userManager, authSettings.AdminEmail, logger);
    await DevPatientSeeder.SeedAsync(dbContext, userManager, devPatientSettings, logger);
    await CommunityDemoSeeder.SeedAsync(dbContext, userManager, communityDemoSettings, logger);
    await PatientAccountDemoSeeder.SeedAsync(dbContext, userManager, patientAccountDemoSettings, logger);
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

// Correlation ID primero: el exception handler y todos los logs del request
// (console y file sink) llevan el CorrelationId en el LogContext.
app.UseMiddleware<CorrelationIdMiddleware>();
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

    // Twilio solo es obligatorio cuando estÃ¡ habilitado: Development puede
    // arrancar sin credenciales (IsEnabled = false en appsettings). Cuando se
    // habilita, las cuatro propiedades son obligatorias para no llegar a
    // runtime con configuraciÃ³n incompleta.
    var twilio = new TwilioSettings();
    configuration.GetSection(TwilioSettings.SectionName).Bind(twilio);

    if (twilio.IsEnabled)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(twilio.AccountSid))
            missing.Add("AccountSid");
        if (string.IsNullOrWhiteSpace(twilio.ApiKeySid))
            missing.Add("ApiKeySid");
        if (string.IsNullOrWhiteSpace(twilio.ApiKeySecret))
            missing.Add("ApiKeySecret");
        if (string.IsNullOrWhiteSpace(twilio.VerifyServiceSid))
            missing.Add("VerifyServiceSid");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Twilio estÃ¡ habilitado (Twilio:IsEnabled) pero faltan las credenciales: "
                    + $"{string.Join(", ", missing)}. ConfigÃºralas (appsettings o variables de "
                    + "entorno TWILIO__*) o deshabilita Twilio."
            );
        }
    }

    // ValidaciÃ³n bÃ¡sica de los lÃ­mites de seguridad del flujo OTP. Solo se
    // comprueban invariantes estructurales (mayor que cero / no negativo); el
    // mecanismo de protecciÃ³n aÃºn no estÃ¡ implementado.
    var otpSecurity = new OtpSecuritySettings();
    configuration.GetSection(OtpSecuritySettings.SectionName).Bind(otpSecurity);

    RequirePositive(otpSecurity.SendPerIpPerMinute, nameof(OtpSecuritySettings.SendPerIpPerMinute));
    RequirePositive(otpSecurity.SendPerIpPerHour, nameof(OtpSecuritySettings.SendPerIpPerHour));
    RequirePositive(
        otpSecurity.SendPerPhonePerMinute,
        nameof(OtpSecuritySettings.SendPerPhonePerMinute)
    );
    RequirePositive(
        otpSecurity.SendPerPhonePerHour,
        nameof(OtpSecuritySettings.SendPerPhonePerHour)
    );
    RequirePositive(otpSecurity.SendPerPhonePerDay, nameof(OtpSecuritySettings.SendPerPhonePerDay));
    RequireNonNegative(
        otpSecurity.SendPhoneCooldownSeconds,
        nameof(OtpSecuritySettings.SendPhoneCooldownSeconds)
    );
    RequirePositive(
        otpSecurity.SendPerDocumentPerHour,
        nameof(OtpSecuritySettings.SendPerDocumentPerHour)
    );
    RequirePositive(
        otpSecurity.VerifyPerIpPerMinute,
        nameof(OtpSecuritySettings.VerifyPerIpPerMinute)
    );
    RequirePositive(
        otpSecurity.VerifyPerPhoneWindowMinutes,
        nameof(OtpSecuritySettings.VerifyPerPhoneWindowMinutes)
    );
    RequirePositive(
        otpSecurity.VerifyPerPhoneLimit,
        nameof(OtpSecuritySettings.VerifyPerPhoneLimit)
    );
    RequirePositive(
        otpSecurity.VerifyPhoneMaxFailedAttempts,
        nameof(OtpSecuritySettings.VerifyPhoneMaxFailedAttempts)
    );
    RequireNonNegative(
        otpSecurity.VerifyPhoneLockoutSeconds,
        nameof(OtpSecuritySettings.VerifyPhoneLockoutSeconds)
    );

    static void RequirePositive(int value, string propertyName)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException(
                $"OtpSecurity:{propertyName} debe ser mayor que 0 (valor actual: {value})."
            );
        }
    }

    static void RequireNonNegative(int value, string propertyName)
    {
        if (value < 0)
        {
            throw new InvalidOperationException(
                $"OtpSecurity:{propertyName} no puede ser negativo (valor actual: {value})."
            );
        }
    }
}
