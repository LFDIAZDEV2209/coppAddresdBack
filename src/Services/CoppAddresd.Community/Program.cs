using System.Text;
using CoppAddresd.Community;
using CoppAddresd.Community.GraphQL;
using CoppAddresd.Community.GraphQL.Mutations;
using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.GraphQL.Resolvers;
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
    options.AddPolicy("Community.Manage", policy =>
        policy.RequireClaim("permission", "Community.Manage"));
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
    .AddTypeExtension<ProfileResolvers>()
    .AddAuthorization()
    .AddInMemorySubscriptions()
    .AddSocketSessionInterceptor(_ => new SubscriptionAuthInterceptor(builder.Configuration));

builder.Services.AddHealthChecks();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CommunityDbContext>();
    await db.Database.MigrateAsync();
    if (app.Environment.IsDevelopment())
        await CommunitySeeder.SeedAsync(db);
}

app.UseCors("CommunityCors");
app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets();

app.MapGraphQL("/api/v1/community/graphql").WithOptions(o => o.Tool.Enable = false);
app.MapHealthChecks("/health");
app.MapGraphQLWebSocket("/api/v1/community/subscriptions");

app.Run();
