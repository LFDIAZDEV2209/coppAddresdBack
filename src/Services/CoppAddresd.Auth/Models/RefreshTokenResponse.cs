namespace CoppAddresd.Auth.Models;

public record RefreshTokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn);