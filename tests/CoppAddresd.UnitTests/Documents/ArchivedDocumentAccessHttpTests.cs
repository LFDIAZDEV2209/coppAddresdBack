using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Api.Security;
using CoppAddresd.Application.Features.Documents;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.UnitTests.Documents;

/// <summary>Rutas MVC reales y política de archivo; repositorios/transporte en
/// memoria, sin BD, proveedores externos, documentos reales ni pacientes reales.</summary>
public sealed class ArchivedDocumentAccessHttpTests
{
    private static readonly Guid Owner = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Other = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Patient = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid DocumentId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ClinicId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid OrganizationId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static string Original => $"documents/{Patient}/{DocumentId}/original.pdf";
    private static string Sidecar => $"documents/{Patient}/{DocumentId}/extraction.json";

    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-User"], out var id)) return Task.FromResult(AuthenticateResult.NoResult());
            var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim("aud", Request.Headers["X-Test-Audience"].ToString())], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(user, Scheme.Name)));
        }
    }

    private sealed class Current(IHttpContextAccessor accessor) : ICurrentContext
    {
        private HttpContext Http => accessor.HttpContext!;
        public Guid? UserId => Guid.TryParse(Http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        public string? SecurityStamp => null;
        public Guid? ActiveClinicId => Guid.TryParse(Http.Request.Headers["X-Clinic-Id"], out var id) ? id : null;
        public Guid? ActiveOrganizationId => Guid.TryParse(Http.Request.Headers["X-Organization-Id"], out var id) ? id : null;
        public Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default) => Task.FromResult(Http.Request.Headers["X-Test-Permission"] == permission);
        public Task<IReadOnlyList<ScopeEntry>> BuildScopeChainAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ScopeEntry>>([]);
        public Task<Guid?> GetProfessionalIdAsync(CancellationToken ct = default) => Task.FromResult<Guid?>(null);
    }

    private sealed class Harness : IAsyncDisposable
    {
        public IDocumentRepository Documents { get; } = Substitute.For<IDocumentRepository>();
        public IPatientRepository Patients { get; } = Substitute.For<IPatientRepository>();
        public IObjectStorageService Storage { get; } = Substitute.For<IObjectStorageService>();
        public StorageSignatureService Signatures { get; } = new("isolated-test-signature-key-not-used-by-any-service");
        public Document Document { get; }
        public HttpClient Client { get; private set; } = null!;
        private WebApplication application = null!;
        private Harness(bool cloud)
        {
            var patient = new PatientProfile { Id = Patient, UserId = Owner, FirstName = "Synthetic", LastName = "Fixture" };
            Document = new Document { Id = DocumentId, PatientId = Patient, Patient = patient, ClinicId = ClinicId,
                Clinic = new Clinic { Id = ClinicId, OrganizationId = OrganizationId }, StorageKey = Original,
                ContentType = "application/pdf", Title = "Synthetic source", DocumentType = new ClinicalDocumentType { Id = Guid.NewGuid(), Name = "Other", Category = new DocumentCategory { Name = "Synthetic" } } };
            Patients.GetByUserIdAsync(Owner, Arg.Any<CancellationToken>()).Returns(patient);
            Patients.GetByUserIdAsync(Other, Arg.Any<CancellationToken>()).Returns(new PatientProfile { Id = Guid.NewGuid(), UserId = Other });
            Documents.GetByIdAsync(DocumentId, Arg.Any<CancellationToken>()).Returns(Document);
            Documents.ListLatestAsync(Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult<(IReadOnlyList<Document> Items, int Total)>(call.ArgAt<Guid?>(0) == Patient ? (new[] { Document }, 1) : (Array.Empty<Document>(), 0)));
            Storage.IsCloudStorage.Returns(cloud);
            Storage.GetPreSignedUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult("https://private-storage.invalid/signed-get"));
            Storage.GetDocumentArchiveReadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult("https://private-storage.invalid/authorized-document-get"));
            Storage.GetDocumentArchiveObjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.7\nSynthetic source only"))));
            Storage.GetObjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.7\nSynthetic source only"))));
        }
        public static async Task<Harness> CreateAsync(bool cloud = false)
        {
            var test = new Harness(cloud);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
            builder.Services.AddSingleton(test.Documents); builder.Services.AddSingleton(test.Patients); builder.Services.AddSingleton(test.Storage); builder.Services.AddSingleton(test.Signatures);
            builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentContext, Current>();
            builder.Services.AddMediatR(configuration => configuration.RegisterServicesFromAssemblyContaining<ListDocumentsQuery>());
            builder.Services.AddAuthentication("DocumentTest").AddScheme<AuthenticationSchemeOptions, Authentication>("DocumentTest", _ => { });
            builder.Services.AddAuthorization(); builder.Services.AddControllers().AddApplicationPart(typeof(StorageController).Assembly);
            test.application = builder.Build(); test.application.UseRouting(); test.application.UseAuthentication(); test.application.UseAuthorization(); test.application.MapControllers();
            await test.application.StartAsync(); test.Client = test.application.GetTestClient(); return test;
        }
        public async Task<HttpResponseMessage> Send(HttpMethod method, string path, Guid? user = null, string audience = "app", string? permission = null, Guid? clinic = null, Guid? organization = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (user.HasValue) { request.Headers.Add("X-Test-User", user.ToString()); request.Headers.Add("X-Test-Audience", audience); }
            if (permission is not null) request.Headers.Add("X-Test-Permission", permission);
            if (clinic.HasValue) request.Headers.Add("X-Clinic-Id", clinic.ToString());
            if (organization.HasValue) request.Headers.Add("X-Organization-Id", organization.ToString());
            return await Client.SendAsync(request);
        }
        public string Signed(string key, bool expired = false)
        {
            var expiry = DateTimeOffset.UtcNow.AddMinutes(expired ? -1 : 5);
            return $"/api/v1/storage/{key}?exp={expiry.ToUnixTimeSeconds()}&sig={Signatures.Sign(key, expiry)}";
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await application.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnPatient_ListsAndDownloadsOriginalWithoutErpPermission(bool cloud)
    {
        await using var test = await Harness.CreateAsync(cloud);
        using var listing = await test.Send(HttpMethod.Get, $"/api/v1/me/documents?page=-5&pageSize=9999&patientId={Other}", Owner);
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);
        using var page = JsonDocument.Parse(await listing.Content.ReadAsStringAsync());
        Assert.Equal(1, page.RootElement.GetProperty("page").GetInt32()); Assert.Equal(100, page.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(DocumentId, Assert.Single(page.RootElement.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        await test.Documents.Received(1).ListLatestAsync(Patient, null, null, null, null, null, 1, 100, Arg.Any<CancellationToken>());
        using var download = await test.Send(HttpMethod.Get, $"/api/v1/me/documents/{DocumentId}/download?key={Uri.EscapeDataString(Sidecar)}", Owner);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        using var json = JsonDocument.Parse(await download.Content.ReadAsStringAsync()); Assert.Equal(900, json.RootElement.GetProperty("expiresInSeconds").GetInt32());
        var url = json.RootElement.GetProperty("url").GetString()!;
        if (cloud) await test.Storage.Received(1).GetDocumentArchiveReadUrlAsync(Original, TimeSpan.FromMinutes(15), Arg.Any<CancellationToken>());
        else {
            using var content = await test.Client.GetAsync(url); Assert.Equal(HttpStatusCode.OK, content.StatusCode);
            Assert.Equal("application/pdf", content.Content.Headers.ContentType?.MediaType); Assert.Contains("no-store", content.Headers.CacheControl!.ToString());
        }
    }

    [Theory]
    [InlineData("original.pdf")]
    [InlineData("extraction.json")]
    public async Task ArchiveSignAndBearerGet_RequireOwnerOrScopedProfessional(string file)
    {
        await using var test = await Harness.CreateAsync(true); var key = $"documents/{Patient}/{DocumentId}/{file}";
        foreach (var operation in new[] { "/api/v1/storage/sign?key=" + Uri.EscapeDataString(key), "/api/v1/storage/" + key }) {
            using var denied = await test.Send(HttpMethod.Get, operation, Other);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        await test.Storage.DidNotReceiveWithAnyArgs().GetObjectAsync(default!, default);
        await test.Storage.DidNotReceiveWithAnyArgs().GetDocumentArchiveObjectAsync(default!, default);
        await test.Storage.DidNotReceiveWithAnyArgs().GetPreSignedUrlAsync(default!, default, default);
        await test.Storage.DidNotReceiveWithAnyArgs().GetDocumentArchiveReadUrlAsync(default!, default, default);
        using var own = await test.Send(HttpMethod.Get, "/api/v1/storage/sign?key=" + Uri.EscapeDataString(key), Owner);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using var ownRead = await test.Send(HttpMethod.Get, "/api/v1/storage/" + key, Owner);
        Assert.Equal(HttpStatusCode.OK, ownRead.StatusCode);
        using var professional = await test.Send(HttpMethod.Get, "/api/v1/storage/sign?key=" + Uri.EscapeDataString(key), Other, "erp", "Documents.View", ClinicId);
        Assert.Equal(HttpStatusCode.OK, professional.StatusCode);
        using var professionalRead = await test.Send(HttpMethod.Get, "/api/v1/storage/" + key, Other, "erp", "Documents.View", ClinicId);
        Assert.Equal(HttpStatusCode.OK, professionalRead.StatusCode);
        using var wrongClinic = await test.Send(HttpMethod.Get, "/api/v1/storage/sign?key=" + Uri.EscapeDataString(key), Other, "erp", "Documents.View", Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, wrongClinic.StatusCode);
        using var wrongOrganization = await test.Send(HttpMethod.Get, "/api/v1/storage/sign?key=" + Uri.EscapeDataString(key), Other, "erp", "Documents.View", null, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, wrongOrganization.StatusCode);
        using var missingPermission = await test.Send(HttpMethod.Get, "/api/v1/storage/" + key, Other, "erp", null, ClinicId);
        Assert.Equal(HttpStatusCode.NotFound, missingPermission.StatusCode);
        using var appCannotBorrowErp = await test.Send(HttpMethod.Get, "/api/v1/storage/" + key, Other, "app", "Documents.View", ClinicId);
        Assert.Equal(HttpStatusCode.NotFound, appCannotBorrowErp.StatusCode);
    }

    [Theory]
    [InlineData("documents/not-a-patient/not-a-document/original.pdf")]
    [InlineData("documents/{p}/{d}/other.json")]
    [InlineData("documents/{p}/{d}/extraction.json/extra")]
    [InlineData("DOCUMENTS/{p}/{d}/extraction.json")]
    [InlineData("documents\\{p}\\{d}\\original.pdf")]
    [InlineData("outside/../documents/{p}/{d}/original.pdf")]
    [InlineData("documents/{p}/{d}/../original.pdf")]
    public async Task MalformedOrUnregisteredArchiveKey_IsNotSigned(string candidate)
    {
        await using var test = await Harness.CreateAsync(true); var key = candidate.Replace("{p}", Patient.ToString()).Replace("{d}", DocumentId.ToString());
        using var response = await test.Send(HttpMethod.Get, "/api/v1/storage/sign?key=" + Uri.EscapeDataString(key), Owner);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await test.Storage.DidNotReceiveWithAnyArgs().GetPreSignedUrlAsync(default!, default, default);
        await test.Storage.DidNotReceiveWithAnyArgs().GetDocumentArchiveReadUrlAsync(default!, default, default);
    }

    [Fact]
    public async Task SignedReadCapability_CannotWriteOrChangeKey_AndExpiredOrInvalidCapabilityFailsClosed()
    {
        await using var test = await Harness.CreateAsync();
        using var put = await test.Send(HttpMethod.Put, test.Signed(Original)); Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        using var ownerPut = await test.Send(HttpMethod.Put, "/api/v1/storage/" + Sidecar, Owner); Assert.Equal(HttpStatusCode.Forbidden, ownerPut.StatusCode);
        await test.Storage.DidNotReceiveWithAnyArgs().PutObjectAsync(default!, default!, default, default);
        using var expired = await test.Send(HttpMethod.Get, test.Signed(Original, expired: true)); Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        using var tampered = await test.Send(HttpMethod.Get, test.Signed(Original).Replace("original.pdf", "extraction.json")); Assert.Equal(HttpStatusCode.Unauthorized, tampered.StatusCode);
        using var malformedExpiry = await test.Send(HttpMethod.Get, "/api/v1/storage/" + Original + "?exp=9223372036854775807&sig=wrong"); Assert.Equal(HttpStatusCode.Unauthorized, malformedExpiry.StatusCode);
        await test.Storage.DidNotReceiveWithAnyArgs().GetObjectAsync(default!, default);
        await test.Storage.DidNotReceiveWithAnyArgs().GetDocumentArchiveObjectAsync(default!, default);
        using var signedSidecar = await test.Send(HttpMethod.Get, test.Signed(Sidecar)); Assert.Equal(HttpStatusCode.OK, signedSidecar.StatusCode);
        Assert.Equal("application/json", signedSidecar.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task WrongPatientDeletedMissingAndUnknownAudience_CannotDownload()
    {
        await using var test = await Harness.CreateAsync();
        using var anonymous = await test.Send(HttpMethod.Get, "/api/v1/me/documents"); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var other = await test.Send(HttpMethod.Get, $"/api/v1/me/documents/{DocumentId}/download", Other); Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        using var erp = await test.Send(HttpMethod.Get, "/api/v1/me/documents", Owner, "erp"); Assert.Equal(HttpStatusCode.Forbidden, erp.StatusCode);
        using var missing = await test.Send(HttpMethod.Get, $"/api/v1/me/documents/{Guid.NewGuid()}/download", Owner); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        test.Document.DeletedAt = DateTime.UtcNow;
        using var deleted = await test.Send(HttpMethod.Get, test.Signed(Original)); Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
        await test.Storage.DidNotReceiveWithAnyArgs().GetObjectAsync(default!, default);
        await test.Storage.DidNotReceiveWithAnyArgs().GetDocumentArchiveObjectAsync(default!, default);
    }

    [Fact]
    public async Task ErpMetadataAlias_CannotMintCapabilityForAnOutsideClinicArchive()
    {
        await using var test = await Harness.CreateAsync(cloud: true);
        test.Document.ClinicId = Guid.NewGuid();
        var alias = new Document { Id = Guid.NewGuid(), PatientId = Other, ClinicId = ClinicId,
            StorageKey = Original, Title = "Synthetic alias", DocumentType = test.Document.DocumentType };
        test.Documents.GetByIdAsync(alias.Id, Arg.Any<CancellationToken>()).Returns(alias);
        test.Documents.ListVersionsAsync(alias.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<Document>());
        using var result = await test.Send(HttpMethod.Get, $"/api/v1/documents/{alias.Id}/download", Other, "erp", "Documents.View", ClinicId);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        await test.Storage.DidNotReceiveWithAnyArgs().GetPreSignedUrlAsync(default!, default, default);
        await test.Storage.DidNotReceiveWithAnyArgs().GetDocumentArchiveReadUrlAsync(default!, default, default);
    }

    [Fact]
    public async Task ExistingGeneralAndFlatErpStorageWorkflowsRemainAvailable()
    {
        await using var test = await Harness.CreateAsync();
        foreach (var key in new[] { "media/videos/fixture.mp4", $"documents/{Patient}/legacy-flat.pdf" }) {
            using var sign = await test.Send(HttpMethod.Get, "/api/v1/storage/sign?key=" + Uri.EscapeDataString(key), Other, "erp"); Assert.Equal(HttpStatusCode.OK, sign.StatusCode);
            using var get = await test.Send(HttpMethod.Get, test.Signed(key)); Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            using var put = await test.Send(HttpMethod.Put, "/api/v1/storage/" + key, Other, "erp"); Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        }
    }
}
