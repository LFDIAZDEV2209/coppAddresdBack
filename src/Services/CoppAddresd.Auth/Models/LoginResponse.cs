namespace CoppAddresd.Auth.Models;

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    string UserId,
    string Email,
    string[] Roles);
