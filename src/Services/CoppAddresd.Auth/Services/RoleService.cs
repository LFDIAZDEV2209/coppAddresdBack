using CoppAddresd.Auth.Contracts;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Services;

public class RoleService : IRoleService
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuthDbContext _dbContext;
    private readonly ITokenInvalidationService _tokenInvalidation;
    private readonly ILogger<RoleService> _logger;

    public RoleService(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        AuthDbContext dbContext,
        ITokenInvalidationService tokenInvalidation,
        ILogger<RoleService> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _dbContext = dbContext;
        _tokenInvalidation = tokenInvalidation;
        _logger = logger;
    }

    public async Task<IEnumerable<RoleResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var roles = await _roleManager.Roles
            .OrderBy(r => r.Name)
            .Select(r => new RoleResponse(
                r.Id.ToString(),
                r.Name!,
                r.Description,
                r.IsActive,
                r.CreatedAt))
            .ToListAsync(ct);

        return roles;
    }

    public async Task<RoleResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role is null) return null;

        return new RoleResponse(
            role.Id.ToString(),
            role.Name!,
            role.Description,
            role.IsActive,
            role.CreatedAt);
    }

    public async Task<(bool Success, string? Error, RoleResponse? Role)> CreateAsync(
        CreateRoleRequest request,
        CancellationToken ct = default)
    {
        var existingRole = await _roleManager.FindByNameAsync(request.Name);
        if (existingRole is not null)
        {
            _logger.LogWarning("Create role failed: name {RoleName} already exists", request.Name);
            return (false, "El nombre del rol ya existe", null);
        }

        var role = new ApplicationRole
        {
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            IsActive = true
        };

        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Create role failed: {Errors}", errors);
            return (false, errors, null);
        }

        _logger.LogInformation("Role {RoleId} created successfully", role.Id);

        var response = new RoleResponse(
            role.Id.ToString(),
            role.Name!,
            role.Description,
            role.IsActive,
            role.CreatedAt);

        return (true, null, response);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(
        Guid id,
        UpdateRoleRequest request,
        CancellationToken ct = default)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role is null)
        {
            return (false, "Rol no encontrado");
        }

        if (request.Name is not null && request.Name != role.Name)
        {
            var existingRole = await _roleManager.FindByNameAsync(request.Name);
            if (existingRole is not null && existingRole.Id != id)
            {
                return (false, "El nombre del rol ya existe");
            }
            role.Name = request.Name;
        }

        if (request.Description is not null)
            role.Description = request.Description;

        if (request.IsActive.HasValue)
            role.IsActive = request.IsActive.Value;

        role.UpdatedAt = DateTime.UtcNow;

        var result = await _roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Update role {RoleId} failed: {Errors}", id, errors);
            return (false, errors);
        }

        _logger.LogInformation("Role {RoleId} updated successfully", id);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role is null)
        {
            return (false, "Rol no encontrado");
        }

        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Delete role {RoleId} failed: {Errors}", id, errors);
            return (false, errors);
        }

        _logger.LogInformation("Role {RoleId} deleted successfully", id);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AssignToUserAsync(
        Guid userId,
        Guid roleId,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return (false, "Usuario no encontrado");
        }

        var role = await _roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return (false, "Rol no encontrado");
        }

        var isInRole = await _userManager.IsInRoleAsync(user, role.Name!);
        if (isInRole)
        {
            return (false, "El usuario ya tiene este rol");
        }

        var result = await _userManager.AddToRoleAsync(user, role.Name!);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Assign role {RoleId} to user {UserId} failed: {Errors}", roleId, userId, errors);
            return (false, errors);
        }

        await _tokenInvalidation.InvalidateUserTokensAsync(userId);

        _logger.LogInformation("Role {RoleId} assigned to user {UserId}", roleId, userId);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RemoveFromUserAsync(
        Guid userId,
        Guid roleId,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return (false, "Usuario no encontrado");
        }

        var role = await _roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return (false, "Rol no encontrado");
        }

        var isInRole = await _userManager.IsInRoleAsync(user, role.Name!);
        if (!isInRole)
        {
            return (false, "El usuario no tiene este rol");
        }

        var result = await _userManager.RemoveFromRoleAsync(user, role.Name!);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Remove role {RoleId} from user {UserId} failed: {Errors}", roleId, userId, errors);
            return (false, errors);
        }

        await _tokenInvalidation.InvalidateUserTokensAsync(userId);

        _logger.LogInformation("Role {RoleId} removed from user {UserId}", roleId, userId);
        return (true, null);
    }

    public async Task<IEnumerable<RoleResponse>> GetUserRolesAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Enumerable.Empty<RoleResponse>();
        }

        var roleNames = await _userManager.GetRolesAsync(user);
        var roles = new List<RoleResponse>();

        foreach (var roleName in roleNames)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role is not null)
            {
                roles.Add(new RoleResponse(
                    role.Id.ToString(),
                    role.Name!,
                    role.Description,
                    role.IsActive,
                    role.CreatedAt));
            }
        }

        return roles;
    }
}
