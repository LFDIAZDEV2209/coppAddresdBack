namespace CoppAddresd.Auth.Models;

/// <summary>Código opaco de un solo uso para abrir la web de eliminación de cuenta.</summary>
public record AccountDeletionHandoffResponse(string Code, int ExpiresInSeconds);
