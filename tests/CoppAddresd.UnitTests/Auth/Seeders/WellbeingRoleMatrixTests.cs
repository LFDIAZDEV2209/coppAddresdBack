using System.Security.Claims;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Seeders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests.Auth.Seeders;

/// <summary>Cada dato de teoría es un rol × fila de ARQUITECTURA §10.1.</summary>
public sealed class WellbeingRoleMatrixTests
{
    public static IEnumerable<object[]> Matrix()
    {
        // Expectativas independientes del catálogo usado por el seed: Admin, director, nutricionista, coordinación.
        (string Operation, string Read, string? Write, string Rights)[] rows =
        [
            ("Perfil 360", "Program.Profile360View", "Program.Edit", "EEVV"),
            ("Plan semanal y rutinas", "Wellness.View", "Program.Edit|Wellness.Manage", "EEVV"),
            ("Asignación masiva y copiar", "Program.Edit", "Program.Edit", "EE--"),
            ("Biblioteca", "Media.View", "Media.Create|Media.Edit|Wellness.Manage", "EEVV"),
            ("Publicar plantilla", "Program.Edit", "Program.Edit", "EE--"),
            ("Ver nutrición", "Nutrition.View", "Nutrition.Manage", "EEEV"),
            ("Editar nutrición", "Nutrition.Manage", "Nutrition.Manage", "EEE-"),
            ("Publicar nutrición", "Nutrition.Approve", "Nutrition.Approve", "EEE-"),
            ("Registros y análisis", "Nutrition.RecordsView", null, "VVVV"),
            ("Fotos y adjuntos", "Nutrition.PhotosView", null, "VVV-"),
            ("Notas internas", "Nutrition.NotesView", "Nutrition.NotesManage", "VVE-"),
            ("Revisión diaria y anclados", "Nutrition.ReviewManage", "Nutrition.ReviewManage", "EEE-"),
            ("IA Perfil 360", "Program.AiView", "Program.AiManage", "EEV-"),
        ];
        string[] roles = ["Admin", "ClinicalDirector", "NutritionReviewer", "CareCoordinator"];
        foreach (var row in rows)
            for (var i = 0; i < roles.Length; i++)
                yield return [roles[i], row.Operation, row.Read, row.Write!, row.Rights[i].ToString()];
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Seed_RoleOperation_MatchesCanonicalMatrix(string role, string operation,
        string read, string? write, string expected)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection().AddLogging().AddDataProtection().Services;
        services.AddDbContext<AuthDbContext>(o => o.UseSqlite(connection));
        services.AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AuthDbContext>().AddDefaultTokenProviders();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await db.Database.EnsureCreatedAsync();
        await PermissionSeeder.SeedAsync(db, NullLogger.Instance);
        await RoleSeeder.SeedAsync(db, NullLogger.Instance);
        await AdminSeeder.SeedAsync(db,
            scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<ApplicationRole>>(),
            new AuthSettings { AdminEmail = "admin@example.invalid", AdminPassword = Guid.NewGuid() + "!aA9" },
            NullLogger.Instance);

        // Una concesión antigua incompatible debe retirarse, no sobrevivir por ser seed aditivo.
        if (role != "Admin")
        {
            var entity = await db.Roles.SingleAsync(r => r.Name == role);
            var existing = await db.RolePermissions.Where(r => r.RoleId == entity.Id).Select(r => r.PermissionId).ToArrayAsync();
            foreach (var permission in await db.Permissions.Where(p => WellbeingRolePermissions.ManagedCodes.Contains(p.Code)
                && !existing.Contains(p.Id)).ToArrayAsync())
                db.RolePermissions.Add(new RolePermission { RoleId = entity.Id, PermissionId = permission.Id });
            await db.SaveChangesAsync();
        }
        await RoleSeeder.SeedAsync(db, NullLogger.Instance);
        await RoleSeeder.SeedAsync(db, NullLogger.Instance);
        var permissions = await db.RolePermissions.AsNoTracking().Where(p => p.Role.Name == role)
            .Select(p => p.Permission.Code).ToArrayAsync();

        async Task<bool> Allowed(string code)
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(permissions.Select(p => new Claim("permission", p)), "test"));
            var requirement = new PermissionRequirement(code);
            var context = new AuthorizationHandlerContext([requirement], user, null);
            await new PermissionHandler(NullLogger<PermissionHandler>.Instance).HandleAsync(context);
            return context.HasSucceeded;
        }
        var canRead = await Allowed(read);
        var canWrite = write is not null;
        foreach (var code in write?.Split('|') ?? []) canWrite &= await Allowed(code);
        var actual = !canRead ? "-" : canWrite ? "E" : "V";
        Assert.True(expected == actual, $"{role} / {operation}: esperado {expected}, recibido {actual}");
        Assert.Equal(permissions.Length, permissions.Distinct().Count());
        if (role != "Admin") Assert.DoesNotContain("Program.View", permissions);
    }
}
