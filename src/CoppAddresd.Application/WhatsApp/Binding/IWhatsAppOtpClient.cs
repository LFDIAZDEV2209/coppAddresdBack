namespace CoppAddresd.Application.WhatsApp.Binding;

public enum OtpOutcome { Approved, Rejected, RateLimited, Unavailable }
public sealed record OtpPhoneLookup(Guid PatientId, string ContactId);
public sealed record OtpLookupResult(OtpOutcome Outcome, OtpPhoneLookup? Phone = null);
public sealed record OtpVerificationResult(OtpOutcome Outcome, Guid? UserId = null);

/// <summary>Solo PHONE de Auth; no expone tokens, cookies ni códigos dev.</summary>
public interface IWhatsAppOtpClient
{
    Task<OtpLookupResult> LookupAsync(string document, CancellationToken ct);
    Task<OtpOutcome> SendAsync(string document, string contactId, string language, CancellationToken ct);
    Task<OtpVerificationResult> VerifyAsync(string document, string code, CancellationToken ct);
}
