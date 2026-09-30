using System.Security.Claims;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests.Media;

/// <summary>
/// Autorización granular de la biblioteca de medios (change
/// erp-program-content-admin, Fase 1). Cubre:
/// 1) El PermissionHandler resuelve el 403 ante ausencia del claim
///    <c>permission</c> requerido (la ausencia del requisito hace que el
///    pipeline de autorización de ASP.NET responda 403 Forbidden) y el 200
///    lógico cuando el claim Media.* está presente en el JWT.
/// 2) Cada acción de <see cref="CoppAddresd.Api.Controllers.MediaController"/>
///    declara el permiso Media.* correcto según la matriz de autorización
///    (reflexión, patrón ProgramSelfServicePermissionTests).
/// 3) Los códigos Media.* están registrados en el catálogo del Auth Service
///    (<c>PermissionCodes.GetAll()</c>).
/// </summary>
public sealed class MediaPermissionTests
{
    // ===================== PermissionHandler (claims del JWT) =====================

    private static ClaimsPrincipal UserWithPermissions(params string[] permissions)
    {
        var claims = permissions
            .Select(p => new Claim(PermissionClaimTypes.Permission, p))
            .ToList();
        claims.Add(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static async Task<bool> AuthorizeAsync(ClaimsPrincipal user, string permissionCode)
    {
        var handler = new PermissionHandler(NullLogger<PermissionHandler>.Instance);
        var requirement = new PermissionRequirement(permissionCode);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    [Theory]
    [InlineData("Media.View")]
    [InlineData("Media.Create")]
    [InlineData("Media.Edit")]
    [InlineData("Media.Publish")]
    [InlineData("Media.Archive")]
    [InlineData("Media.Delete")]
    public async Task Handle_UsuarioConPermisoMedia_Autoriza(string permission)
    {
        // Claim presente en el JWT → la acción proceeds (200/201/204 según verbo).
        var user = UserWithPermissions(permission);
        Assert.True(await AuthorizeAsync(user, permission));
    }

    [Theory]
    [InlineData("Media.View")]
    [InlineData("Media.Create")]
    [InlineData("Media.Edit")]
    [InlineData("Media.Publish")]
    [InlineData("Media.Archive")]
    [InlineData("Media.Delete")]
    public async Task Handle_UsuarioSinPermisoMedia_Niega(string permission)
    {
        // Requisito no satisfecho → el pipeline de autorización responde 403 Forbidden.
        var user = UserWithPermissions("Patients.View", "Program.Edit");
        Assert.False(await AuthorizeAsync(user, permission));
    }

    [Fact]
    public async Task Handle_UsuarioSinClaims_NiegaMediaView()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.False(await AuthorizeAsync(user, "Media.View"));
    }

    // ===================== Matriz de permisos del controlador =====================

    private static System.Reflection.MethodInfo GetAction(string name) =>
        typeof(CoppAddresd.Api.Controllers.MediaController)
            .GetMethods()
            .Single(m => m.Name == name);

    private static string? FindRequirePermission(System.Reflection.MethodInfo method) =>
        method
            .GetCustomAttributes(true)
            .OfType<RequirePermissionAttribute>()
            .FirstOrDefault()
            ?.PermissionCode;

    [Fact]
    public void List_ExigeMediaView() =>
        Assert.Equal("Media.View", FindRequirePermission(GetAction("List")));

    [Fact]
    public void GetById_ExigeMediaView() =>
        Assert.Equal("Media.View", FindRequirePermission(GetAction("GetById")));

    [Fact]
    public void Create_ExigeMediaCreate() =>
        Assert.Equal("Media.Create", FindRequirePermission(GetAction("Create")));

    [Fact]
    public void CreateUploadIntent_ExigeMediaCreate() =>
        Assert.Equal("Media.Create", FindRequirePermission(GetAction("CreateUploadIntent")));

    [Fact]
    public void Update_ExigeMediaEdit() =>
        Assert.Equal("Media.Edit", FindRequirePermission(GetAction("Update")));

    [Fact]
    public void Delete_ExigeMediaDelete() =>
        Assert.Equal("Media.Delete", FindRequirePermission(GetAction("Delete")));

    [Fact]
    public void Controlador_MantieneAuthorizeGlobal() =>
        Assert.True(
            typeof(CoppAddresd.Api.Controllers.MediaController)
                .GetCustomAttributes(true)
                .OfType<AuthorizeAttribute>()
                .Any()
        );

    // ===================== Catálogo de permisos del Auth Service =====================

    [Fact]
    public void AuthPermissionCodes_IncluyenLosSeisCodigosMedia()
    {
        var all = CoppAddresd.Auth.Constants.PermissionCodes.GetAll().ToList();

        Assert.Contains(CoppAddresd.Auth.Constants.PermissionCodes.MediaView, all);
        Assert.Contains(CoppAddresd.Auth.Constants.PermissionCodes.MediaCreate, all);
        Assert.Contains(CoppAddresd.Auth.Constants.PermissionCodes.MediaEdit, all);
        Assert.Contains(CoppAddresd.Auth.Constants.PermissionCodes.MediaPublish, all);
        Assert.Contains(CoppAddresd.Auth.Constants.PermissionCodes.MediaArchive, all);
        Assert.Contains(CoppAddresd.Auth.Constants.PermissionCodes.MediaDelete, all);
    }

    [Fact]
    public void AuthPermissionCodes_MediaDelete_ExisteComoConstante()
    {
        // La eliminación física (media + blobs) queda reservada a Media.Delete.
        Assert.Equal("Media.Delete", CoppAddresd.Auth.Constants.PermissionCodes.MediaDelete);
        Assert.Equal("Media.Publish", CoppAddresd.Auth.Constants.PermissionCodes.MediaPublish);
        Assert.Equal("Media.Archive", CoppAddresd.Auth.Constants.PermissionCodes.MediaArchive);
    }

    // ===================== Seeder idempotente =====================

    [Fact]
    public async Task MediaPermissionsSeeder_CreaSoloLosFaltantes()
    {
        await using var db = new AuthFakeDb();
        var logger = NullLogger.Instance;

        // Primera pasada: crea los 6 códigos.
        await CoppAddresd.Auth.Seeders.MediaPermissionsSeeder.SeedAsync(db.Context, logger);
        Assert.Equal(6, db.Permissions.Count);

        // Segunda pasada: idempotente, no duplica.
        await CoppAddresd.Auth.Seeders.MediaPermissionsSeeder.SeedAsync(db.Context, logger);
        Assert.Equal(6, db.Permissions.Count);
        Assert.Equal(
            6,
            db.Permissions.Count(p => p.Code.StartsWith("Media.", StringComparison.Ordinal))
        );
    }

    /// <summary>
    /// Doble mínimo de <c>AuthDbContext</c> en memoria (EF InMemory): el
    /// seeder solo necesita <c>auth.permissions</c>.
    /// </summary>
    private sealed class AuthFakeDb : IAsyncDisposable
    {
        public AuthFakeDb()
        {
            var options = new DbContextOptionsBuilder<AuthDbContext>()
                .UseInMemoryDatabase($"auth-media-perm-{Guid.NewGuid():N}")
                .Options;
            Context = new AuthDbContext(options);
        }

        public AuthDbContext Context { get; }

        public System.Collections.Generic.List<Permission> Permissions =>
            Context.Permissions.ToList();

        public async ValueTask DisposeAsync() => await Context.DisposeAsync();
    }
}
