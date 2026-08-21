using System.Text;
using CoppAddresd.Community.GraphQL.Mutations;
using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.GraphQL.Subscriptions;
using CoppAddresd.Community.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection no configurada.");

builder.Services.AddDbContext<CommunityDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "community")));

builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpContextAccessor();

var jwt = builder.Configuration.GetSection("Jwt");
var secret = jwt["Secret"]!;
var issuer = jwt["Issuer"]!;
var audiences = jwt.GetSection("ValidAudiences").Get<string[]>() ?? ["app", "erp"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudiences = audiences,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CommunityModerator", policy =>
        policy.RequireClaim("permission", "Community.Moderate"));
});

var origins = builder.Configuration["Cors:Origins"]
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? ["http://localhost:3000", "http://localhost:5173"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("CommunityCors", policy =>
        policy.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader());
});

builder.Services
    .AddGraphQLServer()
    .AddQueryType<CommunityQuery>()
    .AddMutationType<CommunityMutation>()
    .AddSubscriptionType<CommunitySubscription>()
    .AddAuthorization()
    .AddInMemorySubscriptions();

builder.Services.AddHealthChecks();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CommunityDbContext>();
    await db.Database.MigrateAsync();
}

app.UseCors("CommunityCors");
app.UseAuthentication();
app.UseAuthorization();

app.MapGraphQL();
app.MapHealthChecks("/health");
app.MapGraphQLWebSocket();

app.Run();
