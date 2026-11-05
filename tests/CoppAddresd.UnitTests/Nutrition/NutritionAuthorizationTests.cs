using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Api.Middleware;
using CoppAddresd.Api.Security;
using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Features.Nutrition.Engine;
using CoppAddresd.Application.Features.Nutrition.Queries;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class NutritionAuthorizationTests
{
    private static readonly Guid OwnPatient = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ForeignPatient = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Professional = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Owner = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid PlanId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ForeignPlanId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly NutritionPatient Patient = new(68.5, 161, 38, "F", 26.4, 5.9, "MARCHA");

    private sealed class ClaimsContext(IHttpContextAccessor http) : ICurrentContext
    {
        private ClaimsPrincipal User => http.HttpContext!.User;
        public Guid? UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        public string? SecurityStamp => null;
        public Guid? ActiveClinicId => null;
        public Guid? ActiveOrganizationId => null;
        public Task<IReadOnlyList<ScopeEntry>> BuildScopeChainAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ScopeEntry>>([]);
        public Task<bool> HasPermissionAsync(string permissionCode, CancellationToken ct = default) => Task.FromResult(
            User.HasClaim(PermissionClaimTypes.Permission, permissionCode) || User.HasClaim("test_scoped_permission", permissionCode));
        public Task<Guid?> GetProfessionalIdAsync(CancellationToken ct = default) => Task.FromResult<Guid?>(UserId == Owner ? Professional : null);
    }
    private sealed class Harness(IHost host, SymmetricSecurityKey key, List<NutritionPrescription> saved,
        List<NutritionPrescriptionEvent> events) : IDisposable
    {
        public HttpClient Client { get; } = host.GetTestClient();
        public List<NutritionPrescription> Saved => saved;
        public List<NutritionPrescriptionEvent> Events => events;
        public void Authenticate(string mode)
        {
            if (mode == "absent") return;
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, mode == "invalid-id" ? "inválido" : Owner.ToString()) };
            if (mode == "reviewer") claims.Add(new(PermissionClaimTypes.Permission, PermissionCodes.NutritionApprove));
            else if (mode.StartsWith("scoped")) claims.Add(new("test_scoped_permission", PermissionCodes.NutritionManage));
            else if (mode != "none") claims.Add(new(PermissionClaimTypes.Permission, PermissionCodes.NutritionManage));
            if (mode == "global") claims.Add(new(PermissionClaimTypes.Permission, PermissionCodes.PatientsView));
            var jwt = new JwtSecurityToken(audience: mode == "app" ? "app" : "erp", claims: claims,
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(key, SecurityAlgorithms.HmacSha256));
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        }
        public void Dispose() { Client.Dispose(); host.Dispose(); }
    }
    private static async Task<Harness> StartAsync()
    {
        var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        var saved = new List<NutritionPrescription>(); var events = new List<NutritionPrescriptionEvent>();
        var repository = Substitute.For<INutritionPrescriptionRepository>(); var store = Substitute.For<INutritionCaseStore>();
        var inbox = Substitute.For<INutritionInboxRepository>();
        inbox.ListAsync(Arg.Any<NutritionActor>(),Arg.Any<int>(),Arg.Any<int>(),Arg.Any<string?>(),Arg.Any<string?>(),Arg.Any<DateTime>(),Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var actor = call.Arg<NutritionActor>();
                var rows = new List<NutritionInboxRow>();
                if (actor.ViewAll || actor.ProfessionalId == Professional)
                    rows.Add(new(OwnPatient,"Ana Ruiz","sin_plan",null,null,null,false,null,null,null,false,null,null));
                if (actor.ViewAll)
                    rows.Add(new(ForeignPatient,"Luis Prueba","sin_plan",null,null,null,false,null,null,null,false,null,null));
                return new NutritionInboxData(rows,rows.Count);
            });
        repository.ListFoodsAsync(Arg.Any<CancellationToken>()).Returns(NutritionMealSolverTests.Foods().Values.ToArray());
        var plan = new NutritionPrescription { Id = PlanId, PatientId = OwnPatient, CreatedBy = Owner,
            ClinicalProfileJson = JsonSerializer.Serialize(Patient, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            TargetsJson = JsonSerializer.Serialize(NutritionEngine.ComputeTargets(Patient), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Meals = NutritionEngine.MealOrder.Select(m => new NutritionPrescriptionMeal { DayNumber = 1, MealType = m,
                Items = [new() { FoodItemId = "avena", Grams = 40 }] }).ToArray() };
        var foreignPlan = new NutritionPrescription { Id = ForeignPlanId, PatientId = ForeignPatient,
            CreatedBy = Owner, ClinicalProfileJson = plan.ClinicalProfileJson, TargetsJson = plan.TargetsJson, Meals = plan.Meals };
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(c => c.Arg<Guid>() == ForeignPlanId ? foreignPlan : plan);
        repository.When(r => r.AddAsync(Arg.Any<NutritionPrescription>(), Arg.Any<CancellationToken>())).Do(c => saved.Add(c.Arg<NutritionPrescription>()));
        store.GetContextAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new NutritionCaseContext(new(1988, 1, 1), "F", "America/Bogota", false, null));
        store.CanAccessAsync(Arg.Any<Guid>(), Arg.Any<NutritionActor>(), Arg.Any<CancellationToken>()).Returns(c =>
            c.Arg<NutritionActor>().ViewAll || c.Arg<Guid>() == OwnPatient && c.Arg<NutritionActor>().ProfessionalId == Professional);
        store.ExecuteAsync(Arg.Any<Guid>(), Arg.Any<Func<CancellationToken, Task<NutritionView>>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Func<CancellationToken, Task<NutritionView>>>()(c.Arg<CancellationToken>()));
        store.When(s => s.AddEventAsync(Arg.Any<NutritionPrescriptionEvent>(), Arg.Any<CancellationToken>())).Do(c => events.Add(c.Arg<NutritionPrescriptionEvent>()));
        var clinical = Substitute.For<IClinicalContextService>();
        clinical.ConsolidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new ClinicalContextDto(new(38, "F", null), [], [], [], [], new(null, null, null)));
        var drafts = Substitute.For<INutritionDraftClient>();
        drafts.GenerateAsync(Arg.Any<NutritionDraftRequest>(), Arg.Any<CancellationToken>()).Returns(c => new NutritionDraftResponse(new("ES", "EN"), new("ES", "EN"), c.Arg<NutritionDraftRequest>().Menus, "deterministic_fallback"));
        var host = await new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(s =>
        {
            s.AddHttpContextAccessor(); s.AddScoped<ICurrentContext, ClaimsContext>();
            s.AddControllers().AddApplicationPart(typeof(NutritionController).Assembly);
            s.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new()
            { ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true, IssuerSigningKey = key });
            s.AddAuthorization(o => o.AddPolicy(NutritionPolicies.Staff, NutritionPolicies.Configure));
            s.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<NutritionCaseCommand>());
            s.AddScoped<NutritionCases>(); s.AddSingleton(repository); s.AddSingleton(store); s.AddSingleton(clinical);
            s.AddSingleton(drafts); s.AddSingleton(Substitute.For<IProgramAssignmentRepository>()); s.AddSingleton(TimeProvider.System);
            s.AddSingleton(inbox);
        }).Configure(app =>
        {
            app.UseMiddleware<ExceptionHandlingMiddleware>(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            app.UseEndpoints(e => e.MapControllers());
        })).StartAsync();
        return new(host, key, saved, events);
    }

    [Theory]
    [InlineData("absent", 401)] [InlineData("none", 403)] [InlineData("own", 200)]
    [InlineData("reviewer", 200)] [InlineData("foreign", 404)] [InlineData("scoped", 200)]
    [InlineData("global", 200)]
    [InlineData("scoped-foreign", 404)] [InlineData("app", 403)] [InlineData("invalid-id", 403)]
    public async Task HttpRead_EnforcesJwtAudiencePermissionsAndOwnScope(string mode, int expected)
    {
        using var h = await StartAsync(); h.Authenticate(mode);
        var foreign = mode.Contains("foreign") || mode == "global";
        var patient = foreign ? ForeignPatient : OwnPatient; var id = foreign ? ForeignPlanId : PlanId;
        var response = await h.Client.GetAsync($"/api/v1/nutrition/patients/{patient}/prescriptions/{id}");
        Assert.Equal(expected, (int)response.StatusCode);
    }

    [Fact]
    public async Task HttpGenerate_ActorComesFromJwt_AndBodyCannotGrantApproval()
    {
        using var h = await StartAsync(); h.Authenticate("own");
        var response = await h.Client.PostAsJsonAsync($"/api/v1/nutrition/patients/{OwnPatient}/prescriptions", new
        { manual = Patient, birthDate = "1988-01-01", actor = new { userId = Guid.NewGuid(), canApprove = true, viewAll = true } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(Owner, Assert.Single(h.Saved).CreatedBy); Assert.Equal(Owner, Assert.Single(h.Events).ActorUserId);
        response = await h.Client.PostAsJsonAsync($"/api/v1/nutrition/patients/{OwnPatient}/prescriptions/{PlanId}/approve", new
        { revision = 1, criticalConfirmed = true, actor = new { canApprove = true } });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        response = await h.Client.PostAsJsonAsync($"/api/v1/nutrition/patients/{OwnPatient}/prescriptions/{PlanId}/return", new
        { revision = 1, comment = "Solicito revisar", actor = new { canApprove = true } });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void PermissionCatalogs_IncludeBothCapabilitiesWithoutRemovingLegacyPermissions()
    {
        foreach (var code in new[] { PermissionCodes.NutritionManage, PermissionCodes.NutritionApprove, PermissionCodes.NutritionPlansApprove })
        {
            Assert.Contains(code, PermissionCodes.All);
            Assert.Contains(code, CoppAddresd.Auth.Constants.PermissionCodes.GetAll());
        }
        Assert.Equal(PermissionCodes.All.Count, PermissionCodes.All.Distinct().Count());
    }

    [Theory]
    [InlineData("absent",401)] [InlineData("none",403)] [InlineData("own",200)] [InlineData("reviewer",200)]
    [InlineData("scoped",200)] [InlineData("global",200)] [InlineData("app",403)] [InlineData("invalid-id",403)]
    public async Task HttpInbox_RequiresErpAndEitherPermission(string mode,int expected)
    {
        using var h = await StartAsync(); h.Authenticate(mode);
        var response = await h.Client.GetAsync("/api/v1/nutrition/inbox");
        Assert.Equal(expected,(int)response.StatusCode);
    }

    [Theory]
    [InlineData("own",1)] [InlineData("global",2)]
    public async Task HttpInbox_UsesJwtScope_IgnoresClientActor_AndKeepsEnvelope(string mode,int total)
    {
        using var h = await StartAsync(); h.Authenticate(mode);
        var response = await h.Client.GetAsync($"/api/v1/nutrition/inbox?page=1&pageSize=20&viewAll=true&professionalId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("items,total,page,pageSize",string.Join(',',json.EnumerateObject().Select(x => x.Name)));
        Assert.Equal(total,json.GetProperty("total").GetInt32());
        var item = json.GetProperty("items")[0];
        Assert.Equal("patientId,patientName,state,prescription",string.Join(',',item.EnumerateObject().Select(x => x.Name)));
        Assert.Equal(OwnPatient,item.GetProperty("patientId").GetGuid()); Assert.Equal("sin_plan",item.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null,item.GetProperty("prescription").ValueKind);
    }

    [Theory]
    [InlineData("page=0")] [InlineData("pageSize=101")] [InlineData("state=invalid")]
    public async Task HttpInbox_InvalidFilters_Return400(string query)
    {
        using var h = await StartAsync(); h.Authenticate("own");
        Assert.Equal(HttpStatusCode.BadRequest,(await h.Client.GetAsync("/api/v1/nutrition/inbox?"+query)).StatusCode);
    }

    [Theory]
    [InlineData("absent",401)] [InlineData("none",403)] [InlineData("own",200)] [InlineData("reviewer",200)]
    [InlineData("scoped",200)] [InlineData("global",200)] [InlineData("app",403)] [InlineData("invalid-id",403)]
    public async Task HttpFoods_RequiresErpAndEitherPermission(string mode,int expected)
    {
        using var h = await StartAsync(); h.Authenticate(mode);
        Assert.Equal(expected,(int)(await h.Client.GetAsync("/api/v1/nutrition/foods")).StatusCode);
    }

    [Fact]
    public async Task HttpFoods_ReturnsSeededPickerFields_AndHouseholdUnits()
    {
        using var h = await StartAsync(); h.Authenticate("reviewer");
        var response = await h.Client.GetAsync("/api/v1/nutrition/foods"); Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(35,json.GetArrayLength());
        var oats = json.EnumerateArray().Single(x => x.GetProperty("code").GetString()=="avena");
        Assert.Equal("code,name,group,role,kcal100,carbs100,protein100,fat100,fiber100,householdEquivalent",
            string.Join(',',oats.EnumerateObject().Select(x => x.Name)));
        Assert.Equal(379,oats.GetProperty("kcal100").GetDouble());
        Assert.Equal(JsonValueKind.Null,oats.GetProperty("householdEquivalent").ValueKind);
        var bread = json.EnumerateArray().Single(x => x.GetProperty("code").GetString()=="panint").GetProperty("householdEquivalent");
        Assert.Equal("unitSingular,unitPlural,gramsPerUnit,unitStep",string.Join(',',bread.EnumerateObject().Select(x => x.Name)));
        Assert.Equal("rebanada",bread.GetProperty("unitSingular").GetString());
        Assert.Equal(30,bread.GetProperty("gramsPerUnit").GetDouble()); Assert.Equal(.5,bread.GetProperty("unitStep").GetDouble());
    }
}
