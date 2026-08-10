namespace CoppAddresd.Auth.Contracts;

public interface ITokenInvalidationService
{
    Task InvalidateUserTokensAsync(Guid userId, CancellationToken ct = default);
}
