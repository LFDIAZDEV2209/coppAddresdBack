namespace CoppAddresd.Domain.Exceptions;

public sealed class AiConsentException(string code, string scope, int statusCode)
    : Exception(code)
{
    public string Code { get; } = code;
    public string Scope { get; } = scope;
    public int StatusCode { get; } = statusCode;
}
