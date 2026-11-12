using MediatR;

namespace CoppAddresd.Application.Features.Patients;

public sealed record PatientBindingIdentity(Guid PatientId, Guid? UserId, string FirstName, string DocumentNumber,
    string? PhoneCountryCode, string? PhoneNumber);

public interface IPatientBindingQuery
{
    Task<PatientBindingIdentity?> FindByDocumentAsync(string document, CancellationToken ct);
    Task<PatientBindingIdentity?> FindByIdAsync(Guid patientId, CancellationToken ct);
}

public sealed record FindActivePatientForBindingQuery : IRequest<PatientBindingIdentity?>
{
    internal string Document { get; }
    public FindActivePatientForBindingQuery(string document) => Document = document;
    public override string ToString() => nameof(FindActivePatientForBindingQuery);
}
public sealed class FindActivePatientForBindingQueryHandler(IPatientBindingQuery patients)
    : IRequestHandler<FindActivePatientForBindingQuery, PatientBindingIdentity?>
{
    public Task<PatientBindingIdentity?> Handle(FindActivePatientForBindingQuery request, CancellationToken ct) =>
        patients.FindByDocumentAsync(request.Document, ct);
}
