namespace CoppAddresd.Auth.Models;

public record UserResponse(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    DateTime CreatedAt,
    string[] Roles);
