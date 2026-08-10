using CoppAddresd.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddCoppAddresdApplicationServices(builder.Configuration);
builder.Services.ConfigureCors();

builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(CoppAddresd.Api.Extensions.ApplicationServiceExtensions.CorsPolicyName);
app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
