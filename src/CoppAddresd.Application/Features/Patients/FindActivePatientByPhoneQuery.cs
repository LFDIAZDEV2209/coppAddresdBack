using MediatR;

namespace CoppAddresd.Application.Features.Patients;

public sealed record PatientChannelIdentity(Guid PatientId, Guid? UserId, string FirstName);

/// <summary>Consulta del dominio de pacientes; no expone entidades ni datos clínicos al canal.</summary>
public interface IPatientChannelQuery
{
    Task<PatientChannelIdentity?> FindActiveByPhoneAsync(string e164, CancellationToken ct);
}

public sealed record FindActivePatientByPhoneQuery(string Phone) : IRequest<PatientChannelIdentity?>;

public sealed class FindActivePatientByPhoneQueryHandler(IPatientChannelQuery patients)
    : IRequestHandler<FindActivePatientByPhoneQuery, PatientChannelIdentity?>
{
    public Task<PatientChannelIdentity?> Handle(FindActivePatientByPhoneQuery request, CancellationToken ct)
    {
        var normalized = PatientPhone.Normalize(request.Phone);
        return normalized is null ? Task.FromResult<PatientChannelIdentity?>(null) : patients.FindActiveByPhoneAsync(normalized, ct);
    }
}

public static class PatientPhone
{
    public static string? Normalize(string phone, string? countryCode = null)
    {
        if (phone.Any(c => !char.IsAsciiDigit(c) && !"+ ()-.".Contains(c))) return null;
        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal)) digits = digits[2..];
        else if (!phone.TrimStart().StartsWith('+') && digits.Length == 10)
            digits = (countryCode?.TrimStart('+') ?? "57") + digits;
        return digits.Length is >= 8 and <= 15 && digits[0] != '0' ? "+" + digits : null;
    }
}
