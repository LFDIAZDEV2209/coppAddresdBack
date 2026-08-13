namespace CoppAddresd.Auth.Models;

public record LoginResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn);