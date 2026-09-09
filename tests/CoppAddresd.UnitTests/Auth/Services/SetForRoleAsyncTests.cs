using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Services;
using CoppAddresd.Auth.Services.Cache;
using CoppAddresd.UnitTests.Auth.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Auth.Services;

/// <summary>
/// Tests de <see cref="PermissionService.SetForRoleAsync"/>: sincronización
/// total de permisos de un rol (sync-total semantics).
/// </summary>
public sealed class SetForRoleAsyncTests
{
    private sealed class Harness : IDisposable
    {
        private readonly AuthDbContext _db;
        private readonly ITokenInvalidationService _invalidation;
        private readonly ICacheService _cache;
        private readonly PermissionService _service;

        public Harness()
        {
            _db = IdentityTestDoubles.CreateInMemoryDbContext();
            _invalidation = Substitute.For<ITokenInvalidationService>();
            _cache = Substitute.For<ICacheService>();
            _service = new PermissionService(_db, _invalidation, _cache, NullLogger<PermissionService>.Instance);
        }

        public AuthDbContext Db => _db;
        public PermissionService Service => _service;
        public ITokenInvalidationService Invalidation => _invalidation;
        public ICacheService Cache => _cache;

        /// <summary>Crea un rol y retorna su Id.</summary>
        public async Task<Guid> CreateRoleAsync(string name = "TestRole", bool isSystem = false)
        {
            var role = new ApplicationRole
            {
                Name = name,
                Description = "test",
                IsActive = true,
                IsSystem = isSystem,
            };
            _db.Roles.Add(role);
            await _db.SaveChangesAsync();
            return role.Id;
        }

        /// <summary>Crea un permiso y retorna su Id.</summary>
        public async Task<Guid> CreatePermissionAsync(string code = "Test.Permission")
        {
            var permission = new Permission
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = $"Permiso {code}",
                Module = code.Split('.')[0],
            };
            _db.Permissions.Add(permission);
            await _db.SaveChangesAsync();
            return permission.Id;
        }

        /// <summary>Asigna un permiso a un rol.</summary>
        public async Task AssignPermissionToRoleAsync(Guid roleId, Guid permissionId)
        {
            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId,
            });
            await _db.SaveChangesAsync();
        }

        /// <summary>Asigna un usuario al rol.</summary>
        public async Task<Guid> AssignUserToRoleAsync(Guid roleId)
        {
            var userId = Guid.NewGuid();
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"user-{userId:N}@coppaddresd.com",
                Email = $"user-{userId:N}@coppaddresd.com",
                FirstName = "Test",
                LastName = "User",
                IsActive = true,
            };
            _db.Users.Add(user);
            _db.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>
            {
                UserId = userId,
                RoleId = roleId,
            });
            await _db.SaveChangesAsync();
            return userId;
        }

        public void Dispose()
        {
            _db.Dispose();
        }
    }

    [Fact]
    public async Task SetForRoleAsync_Vacio_RoleSinPermisos_EliminaTodos()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync();
        var perm1 = await h.CreatePermissionAsync("Users.View");
        var perm2 = await h.CreatePermissionAsync("Users.Create");
        await h.AssignPermissionToRoleAsync(roleId, perm1);
        await h.AssignPermissionToRoleAsync(roleId, perm2);

        var (success, error) = await h.Service.SetForRoleAsync(roleId, []);

        Assert.True(success);
        Assert.Null(error);
        Assert.Empty(await h.Db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync());
    }

    [Fact]
    public async Task SetForRoleAsync_Vacio_RoleYaSinPermisos_NoOpIdempotente()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync();

        var (success, error) = await h.Service.SetForRoleAsync(roleId, []);

        Assert.True(success);
        Assert.Null(error);
    }

    [Fact]
    public async Task SetForRoleAsync_DeVacioAFull_AgregaPermisos()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync();
        var perm1 = await h.CreatePermissionAsync("Users.View");
        var perm2 = await h.CreatePermissionAsync("Users.Create");

        var (success, error) = await h.Service.SetForRoleAsync(roleId, [perm1, perm2]);

        Assert.True(success);
        Assert.Null(error);
        var assigned = await h.Db.RolePermissions.Where(rp => rp.RoleId == roleId).Select(rp => rp.PermissionId).ToListAsync();
        Assert.Equal(2, assigned.Count);
        Assert.Contains(perm1, assigned);
        Assert.Contains(perm2, assigned);
    }

    [Fact]
    public async Task SetForRoleAsync_MixDiff_AgregaYRemueveCorrectamente()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync();
        var permKeep = await h.CreatePermissionAsync("Users.View");
        var permRemove = await h.CreatePermissionAsync("Users.Create");
        var permAdd = await h.CreatePermissionAsync("Users.Delete");

        // Estado actual: permKeep + permRemove
        await h.AssignPermissionToRoleAsync(roleId, permKeep);
        await h.AssignPermissionToRoleAsync(roleId, permRemove);

        // Objetivo: permKeep + permAdd (remueve permRemove, agrega permAdd)
        var (success, error) = await h.Service.SetForRoleAsync(roleId, [permKeep, permAdd]);

        Assert.True(success);
        Assert.Null(error);
        var assigned = await h.Db.RolePermissions.Where(rp => rp.RoleId == roleId).Select(rp => rp.PermissionId).ToListAsync();
        Assert.Equal(2, assigned.Count);
        Assert.Contains(permKeep, assigned);
        Assert.Contains(permAdd, assigned);
        Assert.DoesNotContain(permRemove, assigned);
    }

    [Fact]
    public async Task SetForRoleAsync_Idempotente_MismoSet_NoOp()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync();
        var perm1 = await h.CreatePermissionAsync("Users.View");
        var perm2 = await h.CreatePermissionAsync("Users.Create");
        await h.AssignPermissionToRoleAsync(roleId, perm1);
        await h.AssignPermissionToRoleAsync(roleId, perm2);

        // Ejecutar el mismo set: no debe haber cambios.
        var (success, error) = await h.Service.SetForRoleAsync(roleId, [perm1, perm2]);

        Assert.True(success);
        Assert.Null(error);
        // No se invalidaron tokens (no hubo cambios).
        await h.Invalidation.DidNotReceive().InvalidateUsersTokensAsync(
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetForRoleAsync_RolNoExiste_RetornaError()
    {
        using var h = new Harness();
        var fakeRoleId = Guid.NewGuid();

        var (success, error) = await h.Service.SetForRoleAsync(fakeRoleId, []);

        Assert.False(success);
        Assert.Equal("Rol no encontrado", error);
    }

    [Fact]
    public async Task SetForRoleAsync_PermisoNoExiste_RetornaError()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync();
        var fakePermId = Guid.NewGuid();

        var (success, error) = await h.Service.SetForRoleAsync(roleId, [fakePermId]);

        Assert.False(success);
        Assert.Equal("Permiso no encontrado", error);
        // No se modificó nada.
        Assert.Empty(await h.Db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync());
    }

    [Fact]
    public async Task SetForRoleAsync_Cambios_InvalidaTokensDeUsuariosDelRol()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync();
        var perm1 = await h.CreatePermissionAsync("Users.View");
        var perm2 = await h.CreatePermissionAsync("Users.Create");
        await h.AssignUserToRoleAsync(roleId);
        await h.AssignUserToRoleAsync(roleId);

        var (success, _) = await h.Service.SetForRoleAsync(roleId, [perm1, perm2]);

        Assert.True(success);
        await h.Invalidation.Received(1).InvalidateUsersTokensAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetForRoleAsync_Cambios_InvalidaCachéDelRol()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync();
        var perm1 = await h.CreatePermissionAsync("Users.View");

        await h.Service.SetForRoleAsync(roleId, [perm1]);

        await h.Cache.Received(1).RemoveAsync(
            AuthCacheKeys.RoleCodes(roleId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetForRoleAsync_PropagaCancellationToken()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync();
        var perm1 = await h.CreatePermissionAsync("Users.View");
        await h.AssignUserToRoleAsync(roleId);
        using var cts = new CancellationTokenSource();

        var (success, _) = await h.Service.SetForRoleAsync(roleId, [perm1], cts.Token);

        Assert.True(success);
        await h.Invalidation.Received(1).InvalidateUsersTokensAsync(
            Arg.Any<IEnumerable<Guid>>(),
            cts.Token);
    }

    [Fact]
    public async Task SetForRoleAsync_MezclaPermisoValidoYInvalido_RetornaError()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync();
        var validPerm = await h.CreatePermissionAsync("Users.View");
        var fakePerm = Guid.NewGuid();

        var (success, error) = await h.Service.SetForRoleAsync(roleId, [validPerm, fakePerm]);

        Assert.False(success);
        Assert.Equal("Permiso no encontrado", error);
        // No se modificó nada (rollback implícito: no se guardó nada).
        Assert.Empty(await h.Db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync());
    }

    [Fact]
    public async Task SetForRoleAsync_RolSinUsuarios_NoIntentaInvalidarTokens()
    {
        using var h = new Harness();
        var roleId = await h.CreateRoleAsync();
        var perm1 = await h.CreatePermissionAsync("Users.View");

        await h.Service.SetForRoleAsync(roleId, [perm1]);

        // No hubo usuarios, así que no se llamó a invalidación.
        await h.Invalidation.DidNotReceive().InvalidateUsersTokensAsync(
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<CancellationToken>());
    }
}
