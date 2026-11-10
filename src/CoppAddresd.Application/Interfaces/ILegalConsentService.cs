using CoppAddresd.Application.Features.LegalDocuments;

namespace CoppAddresd.Application.Interfaces;

public interface ILegalConsentService
{
    Task<LegalConsentStatusDto> GetStatusAsync(Guid userId, string locale, CancellationToken ct);
    Task<LegalConsentReceiptDto> AcceptAsync(Guid userId, AcceptLegalConsentRequest request, CancellationToken ct);
    Task WithdrawAsync(Guid userId, Guid receiptId, CancellationToken ct);
    Task EnsureUserAsync(Guid userId, string scope, CancellationToken ct);
    Task EnsurePatientAsync(Guid patientId, string scope, CancellationToken ct);
}
