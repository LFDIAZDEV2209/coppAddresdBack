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
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

ValidateConfiguration(builder.Configuration);

builder.Services.AddControllers();
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

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<ITokenInvalidationService, TokenInvalidationService>();

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

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
}
