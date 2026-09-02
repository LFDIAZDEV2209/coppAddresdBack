using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using CoppAddresd.Auth.Services;
using CoppAddresd.UnitTests.Auth.TestDoubles;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.UnitTests.Auth.Services;

/// <summary>
/// Verificación de los caminos de invalidación ya implementados
/// (REQ-INVALID-01/02/04): mutación de permisos/roles y cambio de password
/// deben invalidar los tokens del usuario (bump de security stamp).
/// </summary>
public sealed class InvalidationVerificationTests
{
    private sealed class DbSeed
    {
        public AuthDbContext Db { get; }
        public ApplicationUser User { get; }
        public Guid UserId => User.Id;
        public Guid PermissionId { get; } = Guid.NewGuid();
        public Guid RoleId { get; } = Guid.NewGuid();

        public DbSeed(bool seedRoleWithTwoUsers = false, bool seedRolePermission = false)
        {
            Db = IdentityTestDoubles.CreateInMemoryDbContext();

            User = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "staff@coppaddresd.com",
                Email = "staff@coppaddresd.com",
                FirstName = "Staff",
                LastName = "Erp",
                IsActive = true
            };
            Db.Users.Add(User);

            Db.Permissions.Add(new Permission
            {
                Id = PermissionId,
                Code = "Users.View",
                Name = "Ver usuarios",
                Module = "Users"
            });

            if (seedRoleWithTwoUsers)
            {
                var role = new ApplicationRole { Id = RoleId, Name = "Admin" };
                Db.Roles.Add(role);
                Db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = UserId, RoleId = RoleId });
                Db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = Guid.NewGuid(), RoleId = RoleId });

                if (seedRolePermission)
                {
                    Db.RolePermissions.Add(new RolePermission
                    {
                        RoleId = RoleId,
                        PermissionId = PermissionId
                    });
                }
            }

            Db.SaveChanges();
        }
    }

    [Fact]
    public async Task PermissionService_AssignToUserAsync_InvalidatesUserTokens()
    {
        // Arrange: permiso directo al usuario.
        var seed = new DbSeed();
        var invalidation = Substitute.For<ITokenInvalidationService>();
        var service = new PermissionService(seed.Db, invalidation, Substitute.For<CoppAddresd.Auth.Services.Cache.ICacheService>(), NullLogger<PermissionService>.Instance);

        // Act
        var result = await service.AssignToUserAsync(seed.UserId, seed.PermissionId);

        // Assert: el stamp del usuario se bumpeó una vez.
        Assert.True(result.Success);
        await invalidation.Received(1).InvalidateUserTokensAsync(seed.UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PermissionService_AssignToRoleAsync_InvalidatesAllAffectedUsers()
    {
        // Arrange: rol con dos usuarios asignados.
        var seed = new DbSeed(seedRoleWithTwoUsers: true);
        var invalidation = Substitute.For<ITokenInvalidationService>();
        var service = new PermissionService(seed.Db, invalidation, Substitute.For<CoppAddresd.Auth.Services.Cache.ICacheService>(), NullLogger<PermissionService>.Instance);

        // Act
        var result = await service.AssignToRoleAsync(seed.RoleId, seed.PermissionId);

        // Assert: UN solo llamado batch con los 2 usuarios afectados
        // (REQ-INVALID-05: sin loop por usuario).
        Assert.True(result.Success);
        await invalidation.Received(1).InvalidateUsersTokensAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == 2),
            Arg.Any<CancellationToken>());
        await invalidation.DidNotReceive().InvalidateUserTokensAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PermissionService_RemoveFromRoleAsync_InvalidatesAllAffectedUsers_InOneBatch()
    {
        // Arrange: rol con dos usuarios asignados y el permiso ya asignado al rol.
        var seed = new DbSeed(seedRoleWithTwoUsers: true, seedRolePermission: true);
        var invalidation = Substitute.For<ITokenInvalidationService>();
        var service = new PermissionService(seed.Db, invalidation, Substitute.For<CoppAddresd.Auth.Services.Cache.ICacheService>(), NullLogger<PermissionService>.Instance);

        // Act
        var result = await service.RemoveFromRoleAsync(seed.RoleId, seed.PermissionId);

        // Assert: UN solo llamado batch con los 2 usuarios afectados.
        Assert.True(result.Success);
        await invalidation.Received(1).InvalidateUsersTokensAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PermissionService_AssignToRoleAsync_PropagatesCancellationToken()
    {
        // Arrange: el ct del request debe llegar hasta la invalidación batch.
        var seed = new DbSeed(seedRoleWithTwoUsers: true);
        var invalidation = Substitute.For<ITokenInvalidationService>();
        var service = new PermissionService(seed.Db, invalidation, Substitute.For<CoppAddresd.Auth.Services.Cache.ICacheService>(), NullLogger<PermissionService>.Instance);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await service.AssignToRoleAsync(seed.RoleId, seed.PermissionId, cts.Token);

        // Assert: el MISMO token se propagó al servicio de invalidación.
        Assert.True(result.Success);
        await invalidation.Received(1).InvalidateUsersTokensAsync(Arg.Any<IEnumerable<Guid>>(), cts.Token);
    }

    [Fact]
    public async Task PermissionService_AssignToUserAsync_PropagatesCancellationToken()
    {
        // Arrange: invalidación de usuario único también con ct.
        var seed = new DbSeed();
        var invalidation = Substitute.For<ITokenInvalidationService>();
        var service = new PermissionService(seed.Db, invalidation, Substitute.For<CoppAddresd.Auth.Services.Cache.ICacheService>(), NullLogger<PermissionService>.Instance);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await service.AssignToUserAsync(seed.UserId, seed.PermissionId, cts.Token);

        // Assert
        Assert.True(result.Success);
        await invalidation.Received(1).InvalidateUserTokensAsync(seed.UserId, cts.Token);
    }

    [Fact]
    public async Task RoleService_AssignToUserAsync_InvalidatesUserTokens()
    {
        // Arrange: rol Admin asignado a un usuario.
        var seed = new DbSeed();
        var role = new ApplicationRole { Id = seed.RoleId, Name = "Admin" };
        var user = new ApplicationUser { Id = seed.UserId, UserName = "staff@coppaddresd.com" };

        var userManager = IdentityTestDoubles.CreateUserManager(user);
        userManager.IsInRoleAsync(user, "Admin").Returns(false);
        userManager.AddToRoleAsync(user, "Admin").Returns(IdentityResult.Success);

        var roleManager = Substitute.For<RoleManager<ApplicationRole>>(
            Substitute.For<IRoleStore<ApplicationRole>>(),
            Array.Empty<IRoleValidator<ApplicationRole>>(),
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<ApplicationRole>>.Instance);
        roleManager.FindByIdAsync(seed.RoleId.ToString()).Returns(role);

        var invalidation = Substitute.For<ITokenInvalidationService>();
        var service = new RoleService(roleManager, userManager, seed.Db, invalidation, NullLogger<RoleService>.Instance);

        // Act
        var result = await service.AssignToUserAsync(seed.UserId, seed.RoleId);

        // Assert: tokens del usuario invalidados.
        Assert.True(result.Success);
        await invalidation.Received(1).InvalidateUserTokensAsync(seed.UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RoleService_RemoveFromUserAsync_InvalidatesUserTokens()
    {
        // Arrange: remover el rol de un usuario.
        var seed = new DbSeed();
        var role = new ApplicationRole { Id = seed.RoleId, Name = "Admin" };
        var user = new ApplicationUser { Id = seed.UserId, UserName = "staff@coppaddresd.com" };

        var userManager = IdentityTestDoubles.CreateUserManager(user);
        userManager.IsInRoleAsync(user, "Admin").Returns(true);
        userManager.RemoveFromRoleAsync(user, "Admin").Returns(IdentityResult.Success);

        var roleManager = Substitute.For<RoleManager<ApplicationRole>>(
            Substitute.For<IRoleStore<ApplicationRole>>(),
            Array.Empty<IRoleValidator<ApplicationRole>>(),
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<ApplicationRole>>.Instance);
        roleManager.FindByIdAsync(seed.RoleId.ToString()).Returns(role);

        var invalidation = Substitute.For<ITokenInvalidationService>();
        var service = new RoleService(roleManager, userManager, seed.Db, invalidation, NullLogger<RoleService>.Instance);

        // Act
        var result = await service.RemoveFromUserAsync(seed.UserId, seed.RoleId);

        // Assert: tokens del usuario invalidados.
        Assert.True(result.Success);
        await invalidation.Received(1).InvalidateUserTokensAsync(seed.UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RoleService_AssignToUserAsync_PropagatesCancellationToken()
    {
        // Arrange: el ct del request debe llegar a la invalidación del usuario.
        var seed = new DbSeed();
        var role = new ApplicationRole { Id = seed.RoleId, Name = "Admin" };
        var user = new ApplicationUser { Id = seed.UserId, UserName = "staff@coppaddresd.com" };

        var userManager = IdentityTestDoubles.CreateUserManager(user);
        userManager.IsInRoleAsync(user, "Admin").Returns(false);
        userManager.AddToRoleAsync(user, "Admin").Returns(IdentityResult.Success);

        var roleManager = Substitute.For<RoleManager<ApplicationRole>>(
            Substitute.For<IRoleStore<ApplicationRole>>(),
            Array.Empty<IRoleValidator<ApplicationRole>>(),
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<ApplicationRole>>.Instance);
        roleManager.FindByIdAsync(seed.RoleId.ToString()).Returns(role);

        var invalidation = Substitute.For<ITokenInvalidationService>();
        var service = new RoleService(roleManager, userManager, seed.Db, invalidation, NullLogger<RoleService>.Instance);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await service.AssignToUserAsync(seed.UserId, seed.RoleId, cts.Token);

        // Assert: el MISMO token llegó a la invalidación.
        Assert.True(result.Success);
        await invalidation.Received(1).InvalidateUserTokensAsync(seed.UserId, cts.Token);
    }

    [Fact]
    public async Task RoleService_RemoveFromUserAsync_PropagatesCancellationToken()
    {
        // Arrange
        var seed = new DbSeed();
        var role = new ApplicationRole { Id = seed.RoleId, Name = "Admin" };
        var user = new ApplicationUser { Id = seed.UserId, UserName = "staff@coppaddresd.com" };

        var userManager = IdentityTestDoubles.CreateUserManager(user);
        userManager.IsInRoleAsync(user, "Admin").Returns(true);
        userManager.RemoveFromRoleAsync(user, "Admin").Returns(IdentityResult.Success);

        var roleManager = Substitute.For<RoleManager<ApplicationRole>>(
            Substitute.For<IRoleStore<ApplicationRole>>(),
            Array.Empty<IRoleValidator<ApplicationRole>>(),
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<ApplicationRole>>.Instance);
        roleManager.FindByIdAsync(seed.RoleId.ToString()).Returns(role);

        var invalidation = Substitute.For<ITokenInvalidationService>();
        var service = new RoleService(roleManager, userManager, seed.Db, invalidation, NullLogger<RoleService>.Instance);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await service.RemoveFromUserAsync(seed.UserId, seed.RoleId, cts.Token);

        // Assert
        Assert.True(result.Success);
        await invalidation.Received(1).InvalidateUserTokensAsync(seed.UserId, cts.Token);
    }

    [Fact]
    public async Task AuthService_ChangePasswordAsync_BumpsStamp_AndRevokesRefreshTokens()
    {
        // Arrange: usuario con un refresh token activo.
        var seed = new DbSeed();
        seed.Db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = seed.UserId,
            Token = "active-refresh",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            User = seed.User
        });
        seed.Db.SaveChanges();

        var userManager = IdentityTestDoubles.CreateUserManager(seed.User);
        userManager.ChangePasswordAsync(seed.User, "Old@1234", "New@5678").Returns(IdentityResult.Success);

        var authService = new AuthService(
            userManager,
            IdentityTestDoubles.CreateSignInManager(userManager),
            Substitute.For<ITokenService>(),
            Substitute.For<IPermissionService>(),
            Substitute.For<IPatientLookupService>(),
            seed.Db,
            Options.Create(new JwtSettings { AccessTokenExpirationMinutes = 15 }),
            NullLogger<AuthService>.Instance);

        // Act
        var result = await authService.ChangePasswordAsync(seed.UserId, new ChangePasswordRequest
        {
            CurrentPassword = "Old@1234",
            NewPassword = "New@5678"
        });

        // Assert: security stamp bumpeado + refresh tokens activos revocados.
        Assert.True(result.Success);
        await userManager.Received(1).UpdateSecurityStampAsync(seed.User);
        var stored = await seed.Db.RefreshTokens.FirstAsync(rt => rt.Token == "active-refresh");
        Assert.NotNull(stored.RevokedAt);
    }
}
