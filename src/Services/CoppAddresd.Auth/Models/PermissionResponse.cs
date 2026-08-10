namespace CoppAddresd.Auth.Models;

public record PermissionResponse(
    string Id,
    string Code,
    string Name,
    string? Description,
    string Module,
    DateTime CreatedAt);
