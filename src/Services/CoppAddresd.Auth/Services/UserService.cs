using CoppAddresd.Auth.Contracts;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UserService> _logger;

    public UserService(
        UserManager<ApplicationUser> userManager,
        ILogger<UserService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null) return null;

        var roles = await _userManager.GetRolesAsync(user);
        return MapToResponse(user, roles);
    }

    public async Task<IEnumerable<UserResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await _userManager.Users
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync(ct);

        var result = new List<UserResponse>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(MapToResponse(user, roles));
        }

        return result;
    }

    public async Task<(bool Success, string? Error, UserResponse? User)> CreateAsync(
        CreateUserRequest request,
        CancellationToken ct = default)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            _logger.LogWarning("Create user failed: email {Email} already exists", request.Email);
            return (false, "Email ya está registrado", null);
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Create user failed: {Errors}", errors);
            return (false, errors, null);
        }

        _logger.LogInformation("User {UserId} created successfully", user.Id);

        var roles = await _userManager.GetRolesAsync(user);
        return (true, null, MapToResponse(user, roles));
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return (false, "Usuario no encontrado");
        }

        if (request.FirstName is not null)
            user.FirstName = request.FirstName;

        if (request.LastName is not null)
            user.LastName = request.LastName;

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Update user {UserId} failed: {Errors}", id, errors);
            return (false, errors);
        }

        _logger.LogInformation("User {UserId} updated successfully", id);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return (false, "Usuario no encontrado");
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Delete user {UserId} failed: {Errors}", id, errors);
            return (false, errors);
        }

        _logger.LogInformation("User {UserId} deleted successfully", id);
        return (true, null);
    }

    private static UserResponse MapToResponse(ApplicationUser user, IList<string> roles)
    {
        return new UserResponse(
            Id: user.Id.ToString(),
            Email: user.Email!,
            FirstName: user.FirstName,
            LastName: user.LastName,
            IsActive: user.IsActive,
            CreatedAt: user.CreatedAt,
            Roles: roles.ToArray());
    }
}
