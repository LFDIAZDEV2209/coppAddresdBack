namespace CoppAddresd.Auth.Interfaces;

public interface ITokenInvalidationService
{
    Task InvalidateUserTokensAsync(Guid userId, CancellationToken ct = default);
}
