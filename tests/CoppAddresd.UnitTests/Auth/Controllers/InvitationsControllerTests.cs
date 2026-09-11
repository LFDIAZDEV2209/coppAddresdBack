using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Controllers;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.UnitTests.Auth.TestDoubles;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace CoppAddresd.UnitTests.Auth.Controllers;

/// <summary>
/// Tests del endpoint interno de invitaciones (CreateInvitation) con soporte
/// para adoptExisting — vincular usuarios existentes sin duplicar.
/// </summary>
public class InvitationsControllerTests
{
    private readonly IInvitationService _invitations = Substitute.For<IInvitationService>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly ILogger<InvitationsController> _logger =
        Substitute.For<ILogger<InvitationsController>>();
    private readonly IOptions<EmailSettings> _emailOptions =
        Options.Create(new EmailSettings { FrontendUrl = "http://localhost:3000", Provider = "Log" });

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuthDbContext _dbContext;

    // Almacén in-memory para UserManager mock.
    private readonly Dictionary<Guid, ApplicationUser> _userStore = new();
    private readonly Dictionary<string, Guid> _emailIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Guid> _hasPassword = new();

    public InvitationsControllerTests()
    {
        _userManager = CreateFakeUserManager();
        _dbContext = IdentityTestDoubles.CreateInMemoryDbContext();
    }

    private InvitationsController CreateController()
        => new(_invitations, _emailSender, _userManager, _dbContext, _emailOptions, _logger);

    /// <summary>Simula un usuario existente en la BD de Identity (in-memory).</summary>
    private async Task<ApplicationUser> SeedUserAsync(
        string email = "test@example.com",
        bool isActive = true,
        bool hasPassword = false)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = "Ana",
            LastName = "López",
            IsActive = isActive,
            EmailConfirmed = true,
        };

        _userStore[user.Id] = user;
        _emailIndex[email] = user.Id;

        if (hasPassword)
            _hasPassword.Add(user.Id);

        // Crear la app ERP en la BD de AuthDbContext (requisito de EnsureErpAccessAsync).
        var erpApp = await _dbContext.Applications.FirstOrDefaultAsync(a => a.Code == "erp");
        if (erpApp is null)
        {
            erpApp = new CoppAddresd.Auth.Entities.Application
            {
                Id = Guid.NewGuid(),
                Code = "erp",
                Name = "ERP",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            _dbContext.Applications.Add(erpApp!);
            await _dbContext.SaveChangesAsync();
        }

        return user;
    }

    // ─── Tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateInvitation_ExistingUserNoAdopt_Returns409()
    {
        var user = await SeedUserAsync();

        var controller = CreateController();
        var request = new CreateInvitationInternalRequest(user.Email!, "Ana", "López");

        var result = await controller.CreateInvitation(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.NotNull(conflict.Value);
    }

    [Fact]
    public async Task CreateInvitation_AdoptExistingInactive_Returns409()
    {
        var user = await SeedUserAsync(isActive: false);

        var controller = CreateController();
        var request = new CreateInvitationInternalRequest(user.Email!, "Ana", "López", AdoptExisting: true);

        var result = await controller.CreateInvitation(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.NotNull(conflict.Value);
    }

    [Fact]
    public async Task CreateInvitation_AdoptExistingWithoutPassword_Returns200NewInvitation()
    {
        var user = await SeedUserAsync(hasPassword: false);
        var invitationId = Guid.NewGuid();
        var token = "test-token-abc123";

        _invitations.CreateAsync(user.Id, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((true, (string?)null, new Invitation
            {
                Id = invitationId,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddHours(72),
                TokenHash = "hash",
                CreatedAt = DateTime.UtcNow,
            }, token));

        var controller = CreateController();
        var request = new CreateInvitationInternalRequest(user.Email!, "Ana", "López", AdoptExisting: true);

        var result = await controller.CreateInvitation(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(ok.Value);

        // Se creó una nueva invitación y se envió el correo.
        await _invitations.Received(1).CreateAsync(user.Id, Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _emailSender.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateInvitation_AdoptExistingWithPassword_Returns200NoInvitation()
    {
        var user = await SeedUserAsync(hasPassword: true);

        var controller = CreateController();
        var request = new CreateInvitationInternalRequest(user.Email!, "Ana", "López", AdoptExisting: true);

        var result = await controller.CreateInvitation(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(ok.Value);

        // No se creó invitación ni se envió correo.
        await _invitations.DidNotReceive().CreateAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _emailSender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateInvitation_NewEmail_Returns200AdoptedFalse()
    {
        var email = "brand-new@example.com";

        var invitationId = Guid.NewGuid();
        var token = "new-token-xyz";

        _invitations.CreateAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var userId = callInfo.ArgAt<Guid>(0);
                return (true, (string?)null, new Invitation
                {
                    Id = invitationId,
                    UserId = userId,
                    ExpiresAt = DateTime.UtcNow.AddHours(72),
                    TokenHash = "hash",
                    CreatedAt = DateTime.UtcNow,
                }, token);
            });

        var controller = CreateController();
        var request = new CreateInvitationInternalRequest(email, "Nuevo", "Usuario");

        var result = await controller.CreateInvitation(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(ok.Value);

        // Se creó invitación y se envió correo.
        await _invitations.Received(1).CreateAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _emailSender.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    // ─── Helpers ──────────────────────────────────────────────────────

    private UserManager<ApplicationUser> CreateFakeUserManager()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var opts = Options.Create(new IdentityOptions());
        var hasher = Substitute.For<IPasswordHasher<ApplicationUser>>();
        var normalizer = Substitute.For<ILookupNormalizer>();
        var errors = new IdentityErrorDescriber();
        var services = Substitute.For<IServiceProvider>();
        var logger = Substitute.For<ILogger<UserManager<ApplicationUser>>>();

        var manager = Substitute.For<UserManager<ApplicationUser>>(
            store, opts, hasher,
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            normalizer, errors, services, logger);

        // FindByIdAsync
        manager.FindByIdAsync(Arg.Any<string>()).Returns(callInfo =>
        {
            var id = callInfo.Arg<string>();
            return Guid.TryParse(id, out var guid) && _userStore.TryGetValue(guid, out var user)
                ? user
                : null!;
        });

        // FindByEmailAsync
        manager.FindByEmailAsync(Arg.Any<string>()).Returns(callInfo =>
        {
            var email = callInfo.Arg<string>();
            return _emailIndex.TryGetValue(email, out var uid) && _userStore.TryGetValue(uid, out var user)
                ? user
                : null!;
        });

        // CreateAsync
        manager.CreateAsync(Arg.Any<ApplicationUser>()).Returns(callInfo =>
        {
            var user = callInfo.Arg<ApplicationUser>();
            _userStore[user.Id] = user;
            if (user.Email is not null)
                _emailIndex[user.Email] = user.Id;
            return IdentityResult.Success;
        });

        // HasPasswordAsync
        manager.HasPasswordAsync(Arg.Any<ApplicationUser>()).Returns(callInfo =>
        {
            var user = callInfo.Arg<ApplicationUser>();
            return Task.FromResult(_hasPassword.Contains(user.Id));
        });

        return manager;
    }
}
