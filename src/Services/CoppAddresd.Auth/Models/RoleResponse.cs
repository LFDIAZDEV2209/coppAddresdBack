namespace CoppAddresd.Auth.Models;

public record RoleResponse(
    string Id,
    string Name,
    string? Description,
    bool IsActive,
    bool IsSystem,
    DateTime CreatedAt
);
